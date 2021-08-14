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
	nanoseconds total_runtime = nanoseconds(0);
};

struct StackEntry
{
	void* method;
	time_point<high_resolution_clock> entry_time;
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
		stack.push_back(StackEntry{ method, high_resolution_clock::now() });
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
	}

	table_t get_table()
	{
		std::lock_guard guard(stats_mut);
		table_t ret(std::move(table));
		table.clear();
		return ret;
	}

	static void dump()
	{
		table_t total_table;
		{
			std::lock_guard guard(all_instances_mut);
			for (auto& thread_info : all_instances)
			{
				table_t thread_table(thread_info->get_table());
				for (const auto& entry : thread_table)
				{
					auto it = total_table.find(entry.first);
					if (it == total_table.end())
						total_table.insert(entry);
					else
					{
						it->second.call_count += entry.second.call_count;
						it->second.total_runtime += entry.second.total_runtime;
					}
				}
			}
		}

		std::ofstream fs;

		fs.open("MonoProfilerOutput.csv", std::fstream::out | std::fstream::trunc);

		std::vector<std::pair<void*, MethodStats>> entries(total_table.begin(), total_table.end());

		//Sort by time
		sort(entries.begin(), entries.end(), [=](auto& a, auto& b) {
			return a.second.total_runtime.count() > b.second.total_runtime.count();
		});

		fs << "\"Call count\",\"Method name\",\"Total runtime (ns)\"" << std::endl;

		//Dump into csv
		for (auto& it : entries)
		{
			fs << it.second.call_count << ",\"" << mono_method_full_name(it.first) << "\"," << it.second.total_runtime.count() << std::endl;
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
