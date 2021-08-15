// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include "dllmain.h"

#include <set>
#include <algorithm>
#include <vector>
#include <functional>
#include <mutex>

using namespace std::chrono;

struct MethodStats
{
	uint64_t call_count = 0;
	uint64_t total_allocation = 0;
	nanoseconds total_runtime = nanoseconds(0);
};

struct StackEntry
{
	void* method;
	time_point<high_resolution_clock> entry_time;
	uint64_t entry_alloc;
};

// Per-thread profiler info.
struct ThreadProfilerInfo
{
	using table_t = std::unordered_map<void*, MethodStats>;
	std::mutex stats_mut;
	table_t table; // Needs lock: stats_mut

	const uint32_t thread_id;

	std::vector<StackEntry> stack; // Used exclusively by the owner thread. Needs lock: none

	static std::mutex all_instances_mut;
	static std::set<ThreadProfilerInfo*> all_instances; // Needs lock: all_instances_mut

	ThreadProfilerInfo()
		: thread_id(mono_thread_current()->small_id)
	{
		stack.reserve(100);

		std::lock_guard guard(all_instances_mut);
		all_instances.insert(this);
	}

	~ThreadProfilerInfo()
	{
		std::lock_guard guard(all_instances_mut);
		all_instances.erase(this);
	}

	void enter_method(void* method)
	{
		stack.push_back(StackEntry{ method, high_resolution_clock::now(), mono_gc_get_used_size() });
	}

	void leave_method(void* method)
	{
		auto now = high_resolution_clock::now();
		if (stack.empty())
			return;

		StackEntry top = stack.back();
		stack.pop_back();

		if (method != top.method)
			return;

		std::lock_guard guard(stats_mut);
		table_t::iterator it = table.find(method);
		if (it == table.end())
		{
			it = table.insert({ method, {} }).first;
		}

		it->second.total_runtime += now - top.entry_time;
		it->second.call_count++;
		uint64_t used_size = mono_gc_get_used_size();
		// If a GC has happened since the method was entered, our allocation
		// estimate will be messed up. Here we use a simple heuristic:
		// ignore any negative allocation number.
		if (used_size > top.entry_alloc)
		{
			it->second.total_allocation += used_size - top.entry_alloc;
		}
	}

	table_t get_table()
	{
		std::lock_guard guard(stats_mut);
		table_t ret(std::move(table));
		table.clear();
		return ret;
	}

	struct Row
	{
		uint32_t thread_id;
		const char* name;
		uint64_t count;
		int64_t total_runtime;
		uint64_t total_allocation;
	};

	static void dump()
	{
		std::vector<Row> rows;
		{
			std::lock_guard guard(all_instances_mut);
			for (auto& thread_info : all_instances)
			{
				table_t thread_table(thread_info->get_table());
				for (const auto& entry : thread_table)
				{
					rows.push_back(Row{
						.thread_id = thread_info->thread_id,
						.name = mono_method_full_name(entry.first),
						.count = entry.second.call_count,
						.total_runtime = entry.second.total_runtime.count(),
						.total_allocation = entry.second.total_allocation });
				}
			}
		}

		std::ofstream fs;

		fs.open("MonoProfilerOutput.csv", std::fstream::out | std::fstream::trunc);


		//Sort by time
		sort(rows.begin(), rows.end(), [=](auto& a, auto& b) {
			return a.total_runtime > b.total_runtime;
		});

		fs << "\"Thread\",\"Call count\",\"Method name\",\"Total runtime (ns)\",\"Total allocation (bytes)\"" << std::endl;

		//Dump into csv
		for (auto& it : rows)
		{
			fs << it.thread_id << "," << it.count << ",\"" << it.name << "\"," <<
				it.total_runtime << "," << it.total_allocation << std::endl;
		}

		fs.close();
	}
};

std::mutex ThreadProfilerInfo::all_instances_mut;
std::set<ThreadProfilerInfo*> ThreadProfilerInfo::all_instances;

static thread_local ThreadProfilerInfo* thread_profiler_info;

static void shutdown(void* prof)
{
	//dump();
}

static void method_enter(void* prof, void* method)
{
	if (!thread_profiler_info)
	{
		thread_profiler_info = new ThreadProfilerInfo();
	}
	thread_profiler_info->enter_method(method);
}


static void method_leave(void* prof, void* method)
{
	if (thread_profiler_info)
		thread_profiler_info->leave_method(method);
}

static void thread_detach()
{
	if (thread_profiler_info)
		delete thread_profiler_info;
	thread_profiler_info = nullptr;
}

extern "C" void __declspec(dllexport) AddProfiler(HMODULE mono)
{
	init_mono_funcs(mono);

	//Install profiler, shutdown doesn't fire so do this manually on DLL_PROCESS_DETACH
	//prof = new MonoProfiler();
	mono_profiler_install(NULL, NULL);
	mono_profiler_install_enter_leave(method_enter, method_leave);
	mono_profiler_set_events(MONO_PROFILE_ENTER_LEAVE);
}

extern "C" void __declspec(dllexport) Dump()
{
	ThreadProfilerInfo::dump();
}


BOOL WINAPI DllMain(HINSTANCE /* hInstDll */, DWORD reasonForDllLoad, LPVOID /* reserved */)
{
	if (reasonForDllLoad == DLL_PROCESS_DETACH)
		shutdown(NULL);
	else if (reasonForDllLoad == DLL_THREAD_DETACH)
		thread_detach();
	return TRUE;
}
