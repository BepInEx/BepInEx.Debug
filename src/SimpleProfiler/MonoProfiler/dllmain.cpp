// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include "dllmain.h"

#include <set>
#include <algorithm>
#include <vector>
#include <functional>
#include <mutex>

std::mutex mut;

static void dump()
{
	mut.lock();
	std::ofstream fs;

	fs.open("MonoProfilerOutput.csv", std::fstream::out | std::fstream::trunc);

	std::vector<std::reference_wrapper<ProfilerInfo>> infos;
	for (auto&[key, val] : profilerInfo)
		infos.push_back(std::ref(val));

	//Lambda sort on profilerinfo.calls
	sort(infos.begin(), infos.end(), [=](ProfilerInfo& a, ProfilerInfo& b) {
		return a.totalRuntime.count() > b.totalRuntime.count();
	});

	fs << "\"Call count\"" << ",\"" << "\"Method name\"" << "\"," << "\"Total runtime (ns)\"" << std::endl;

	//Dump into csv
	for (auto& it : infos)
	{
		auto pi = it.get();
		//auto rt = duration_cast<milliseconds>(pi.totalRuntime).count();
		fs << pi.calls << ",\"" << pi.name << "\"," << pi.totalRuntime.count() << std::endl;
	}

	fs.close();

	profilerInfo.clear();

	mut.unlock();
}

static void shutdown(void* prof)
{
	dump();
}

static void method_enter(void* prof, void* method)
{
	mut.lock();
	if (method)
	{
		auto it = profilerInfo.find(method);
		if (it == profilerInfo.end())
			profilerInfo[method] = ProfilerInfo(method);
		else
			it->second.calls++;
	}
	mut.unlock();
}


static void method_leave(void* prof, void* method)
{
	mut.lock();
	if (method)
	{
		auto it = profilerInfo.find(method);
		if (it != profilerInfo.end())
			it->second.pop_thread();
	}
	mut.unlock();
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
	dump();
}


BOOL WINAPI DllMain(HINSTANCE /* hInstDll */, DWORD reasonForDllLoad, LPVOID /* reserved */)
{
	if (reasonForDllLoad == DLL_PROCESS_DETACH)
		shutdown(NULL);
	return TRUE;
}
