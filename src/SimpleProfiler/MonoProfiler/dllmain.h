#pragma once

#include <chrono>
#include <map>
#include <fstream>
#include <time.h>
#include <Windows.h> // Windows Platform SDK

using namespace std::chrono;

#define MONO_FUN(name, ret, ...) \
	typedef ret (*name##_t)(__VA_ARGS__); \
	static name##_t name;


typedef struct {
	void* vtable;
	void* synchronisation;
} MonoObject;

typedef char MonoBoolean;
typedef void* gpointer;
typedef unsigned __int16	gunichar2;
typedef unsigned __int8		guint8;
typedef unsigned __int32	guint32;
typedef __int32				gint32;
typedef unsigned __int64	guint64;
typedef unsigned long gsize;

struct _MonoThread {
	MonoObject  obj;
	int         lock_thread_id; /* to be used as the pre-shifted thread id in thin locks */
	HANDLE	    handle;
	void* cached_culture_info;
	gpointer    unused1;
	MonoBoolean threadpool_thread;
	gunichar2* name;
	guint32	    name_len;
	guint32	    state;
	/* MonoException* */ void* abort_exc;
	int abort_state_handle;
	guint64 tid;	/* This is accessed as a gsize in the code (so it can hold a 64bit pointer on systems that need it), but needs to reserve 64 bits of space on all machines as it corresponds to a field in managed code */
	HANDLE	    start_notify;
	gpointer stack_ptr;
	gpointer* static_data;
	gpointer jit_data;
	gpointer lock_data;
	/* MonoAppContext* */ void* current_appcontext;
	int stack_size;
	MonoObject* start_obj;
	/* GSList* */ void* appdomain_refs;
	/* This is modified using atomic ops, so keep it a gint32 */
	gint32 interruption_requested;
	gpointer suspend_event;
	gpointer suspended_event;
	gpointer resume_event;
	CRITICAL_SECTION* synch_cs;
	guint8* serialized_culture_info;
	guint32 serialized_culture_info_len;
	guint8* serialized_ui_culture_info;
	guint32 serialized_ui_culture_info_len;
	MonoBoolean thread_dump_requested;
	gpointer end_stack; /* This is only used when running in the debugger. */
	MonoBoolean thread_interrupt_requested;
	guint8	apartment_state;
	gint32 critical_region_level;
	guint32 small_id; /* A small, unique id, used for the hazard pointer table. */
	/* MonoThreadManageCallback* */ void* manage_callback;
	/* MonoException* */ void* pending_exception;
	MonoObject* ec_to_set;
	/*
	 * These fields are used to avoid having to increment corlib versions
	 * when a new field is added to the unmanaged MonoThread structure.
	 */
	gpointer interrupt_on_stop;
	gsize    flags;
	gpointer unused4;
	gpointer unused5;
	gpointer unused6;
	MonoObject* threadstart;
	int managed_id;
	MonoObject* principal;
};

MONO_FUN(mono_thread_current, _MonoThread*);
MONO_FUN(mono_method_full_name, char*, void* method);

struct ProfilerInfo
{
	std::map<uint32_t, time_point<steady_clock>> threadTiming;

	char* name;
	uint64_t calls = 1;

	nanoseconds totalRuntime = nanoseconds(0);

	ProfilerInfo() : name((char*)"__NULL__") { }

	ProfilerInfo(void* method)
	{
		name = mono_method_full_name(method);
		push_thread();
	}

	void push_thread()
	{
		threadTiming[mono_thread_current()->small_id] = high_resolution_clock::now();
	}

	void pop_thread()
	{
		auto k = threadTiming.find(mono_thread_current()->small_id);
		if (k != threadTiming.end())
		{
			totalRuntime += (high_resolution_clock::now() - k->second);
			threadTiming.erase(k);
		}
	}
};

//struct MonoProfiler
//{
//	std::map<void*, ProfilerInfo> profilerInfo;
//};

//static MonoProfiler* prof;
static std::map<void*, ProfilerInfo> profilerInfo;

typedef enum
{
	MONO_PROFILE_NONE = 0,
	MONO_PROFILE_APPDOMAIN_EVENTS = 1 << 0,
	MONO_PROFILE_ASSEMBLY_EVENTS = 1 << 1,
	MONO_PROFILE_MODULE_EVENTS = 1 << 2,
	MONO_PROFILE_CLASS_EVENTS = 1 << 3,
	MONO_PROFILE_JIT_COMPILATION = 1 << 4,
	MONO_PROFILE_INLINING = 1 << 5,
	MONO_PROFILE_EXCEPTIONS = 1 << 6,
	MONO_PROFILE_ALLOCATIONS = 1 << 7,
	MONO_PROFILE_GC = 1 << 8,
	MONO_PROFILE_THREADS = 1 << 9,
	MONO_PROFILE_REMOTING = 1 << 10,
	MONO_PROFILE_TRANSITIONS = 1 << 11,
	MONO_PROFILE_ENTER_LEAVE = 1 << 12,
	MONO_PROFILE_COVERAGE = 1 << 13,
	MONO_PROFILE_INS_COVERAGE = 1 << 14,
	MONO_PROFILE_STATISTICAL = 1 << 15,
	MONO_PROFILE_METHOD_EVENTS = 1 << 16,
	MONO_PROFILE_MONITOR_EVENTS = 1 << 17,
	MONO_PROFILE_IOMAP_EVENTS = 1 << 18,
	/* this should likely be removed, too */
	MONO_PROFILE_GC_MOVES = 1 << 19
} MonoProfileFlags;

typedef void (*MonoProfileFunc)(void* prof);
typedef void (*MonoProfileMethodFunc)(void* prof, void* method);

MONO_FUN(mono_profiler_install, void, void* prof, MonoProfileFunc shutdown_callback);
MONO_FUN(mono_profiler_set_events, void, MonoProfileFlags events);
MONO_FUN(mono_profiler_install_enter_leave, void, MonoProfileMethodFunc enter, MonoProfileMethodFunc fleave);

inline void init_mono_funcs(HMODULE mono)
{
#define GET_FUN(name) name = reinterpret_cast<name##_t>(GetProcAddress(mono, #name));

	GET_FUN(mono_method_full_name);
	GET_FUN(mono_profiler_install);
	GET_FUN(mono_profiler_set_events);
	GET_FUN(mono_profiler_install_enter_leave);
	GET_FUN(mono_thread_current);

#undef GET_FUN
}

static void method_leave(void* prof, void* method);
