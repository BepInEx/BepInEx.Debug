#pragma once

//TODO: Consider removing Mono stuff because it's not needed (unless you want some extra info) and because it's likely not portable between mono versions

typedef unsigned int   guint;
typedef unsigned __int8		guint8;
typedef __int16				gint16;
typedef unsigned __int16	guint16;
typedef __int32				gint32;
typedef unsigned __int32	guint32;

#ifndef MONO_ZERO_LEN_ARRAY
#ifdef __GNUC__
#define MONO_ZERO_LEN_ARRAY 0
#else
#define MONO_ZERO_LEN_ARRAY 1
#endif
#endif

#define USE_UINT8_BIT_FIELD(type, field) type field

typedef struct _MonoClass MonoClass;
typedef struct _MonoDomain MonoDomain;
typedef struct _MonoMethod MonoMethod;
typedef struct _MonoType MonoType;
typedef struct _MonoMethodSignature MonoMethodSignature;
typedef struct _MonoArrayType MonoArrayType;

typedef enum {
	MONO_TYPE_END = 0x00,       /* End of List */
	MONO_TYPE_VOID = 0x01,
	MONO_TYPE_BOOLEAN = 0x02,
	MONO_TYPE_CHAR = 0x03,
	MONO_TYPE_I1 = 0x04,
	MONO_TYPE_U1 = 0x05,
	MONO_TYPE_I2 = 0x06,
	MONO_TYPE_U2 = 0x07,
	MONO_TYPE_I4 = 0x08,
	MONO_TYPE_U4 = 0x09,
	MONO_TYPE_I8 = 0x0a,
	MONO_TYPE_U8 = 0x0b,
	MONO_TYPE_R4 = 0x0c,
	MONO_TYPE_R8 = 0x0d,
	MONO_TYPE_STRING = 0x0e,
	MONO_TYPE_PTR = 0x0f,       /* arg: <type> token */
	MONO_TYPE_BYREF = 0x10,       /* arg: <type> token */
	MONO_TYPE_VALUETYPE = 0x11,       /* arg: <type> token */
	MONO_TYPE_CLASS = 0x12,       /* arg: <type> token */
	MONO_TYPE_VAR = 0x13,	   /* number */
	MONO_TYPE_ARRAY = 0x14,       /* type, rank, boundsCount, bound1, loCount, lo1 */
	MONO_TYPE_GENERICINST = 0x15,	   /* <type> <type-arg-count> <type-1> \x{2026} <type-n> */
	MONO_TYPE_TYPEDBYREF = 0x16,
	MONO_TYPE_I = 0x18,
	MONO_TYPE_U = 0x19,
	MONO_TYPE_FNPTR = 0x1b,	      /* arg: full method signature */
	MONO_TYPE_OBJECT = 0x1c,
	MONO_TYPE_SZARRAY = 0x1d,       /* 0-based one-dim-array */
	MONO_TYPE_MVAR = 0x1e,       /* number */
	MONO_TYPE_CMOD_REQD = 0x1f,       /* arg: typedef or typeref token */
	MONO_TYPE_CMOD_OPT = 0x20,       /* optional arg: typedef or typref token */
	MONO_TYPE_INTERNAL = 0x21,       /* CLR internal type */

	MONO_TYPE_MODIFIER = 0x40,       /* Or with the following types */
	MONO_TYPE_SENTINEL = 0x41,       /* Sentinel for varargs method signature */
	MONO_TYPE_PINNED = 0x45,       /* Local var that points to pinned object */

	MONO_TYPE_ENUM = 0x55        /* an enumeration */
} MonoTypeEnum;

typedef struct {
	unsigned int required : 1;
	unsigned int token : 31;
} MonoCustomMod;

struct _MonoType {
	union {
		MonoClass* klass; /* for VALUETYPE and CLASS */
		MonoType* type;   /* for PTR */
		MonoArrayType* array; /* for ARRAY */
		MonoMethodSignature* method;
		void* generic_param; /* for VAR and MVAR */
		void* generic_class; /* for GENERICINST */
	} data;
	unsigned int attrs : 16; /* param attributes or field flags */
	MonoTypeEnum type : 8;
	unsigned int num_mods : 6;  /* max 64 modifiers follow at the end */
	unsigned int byref : 1;
	unsigned int pinned : 1;  /* valid when included in a local var signature */
	MonoCustomMod modifiers[MONO_ZERO_LEN_ARRAY]; /* this may grow */
};

struct _MonoClass {
	/* element class for arrays and enum basetype for enums */
	MonoClass* element_class;
	/* used for subtype checks */
	MonoClass* cast_class;

	/* for fast subtype checks */
	MonoClass** supertypes;
	guint16     idepth;

	/* array dimension */
	guint8     rank;

	int        instance_size; /* object instance size */

	USE_UINT8_BIT_FIELD(guint, inited          : 1);
	/* We use init_pending to detect cyclic calls to mono_class_init */
	USE_UINT8_BIT_FIELD(guint, init_pending    : 1);

	/* A class contains static and non static data. Static data can be
	 * of the same type as the class itselfs, but it does not influence
	 * the instance size of the class. To avoid cyclic calls to
	 * mono_class_init (from mono_class_instance_size ()) we first
	 * initialise all non static fields. After that we set size_inited
	 * to 1, because we know the instance size now. After that we
	 * initialise all static fields.
	 */
	USE_UINT8_BIT_FIELD(guint, size_inited     : 1);
	USE_UINT8_BIT_FIELD(guint, valuetype       : 1); /* derives from System.ValueType */
	USE_UINT8_BIT_FIELD(guint, enumtype        : 1); /* derives from System.Enum */
	USE_UINT8_BIT_FIELD(guint, blittable       : 1); /* class is blittable */
	USE_UINT8_BIT_FIELD(guint, unicode         : 1); /* class uses unicode char when marshalled */
	USE_UINT8_BIT_FIELD(guint, wastypebuilder  : 1); /* class was created at runtime from a TypeBuilder */
	/* next byte */
	guint8 min_align;
	/* next byte */
	USE_UINT8_BIT_FIELD(guint, packing_size    : 4);
	/* still 4 bits free */
	/* next byte */
	USE_UINT8_BIT_FIELD(guint, ghcimpl         : 1); /* class has its own GetHashCode impl */
	USE_UINT8_BIT_FIELD(guint, has_finalize    : 1); /* class has its own Finalize impl */
	USE_UINT8_BIT_FIELD(guint, marshalbyref    : 1); /* class is a MarshalByRefObject */
	USE_UINT8_BIT_FIELD(guint, contextbound    : 1); /* class is a ContextBoundObject */
	USE_UINT8_BIT_FIELD(guint, delegate        : 1); /* class is a Delegate */
	USE_UINT8_BIT_FIELD(guint, gc_descr_inited : 1); /* gc_descr is initialized */
	USE_UINT8_BIT_FIELD(guint, has_cctor       : 1); /* class has a cctor */
	USE_UINT8_BIT_FIELD(guint, has_references  : 1); /* it has GC-tracked references in the instance */
	/* next byte */
	USE_UINT8_BIT_FIELD(guint, has_static_refs : 1); /* it has static fields that are GC-tracked */
	USE_UINT8_BIT_FIELD(guint, no_special_static_fields : 1); /* has no thread/context static fields */
	/* directly or indirectly derives from ComImport attributed class.
	 * this means we need to create a proxy for instances of this class
	 * for COM Interop. set this flag on loading so all we need is a quick check
	 * during object creation rather than having to traverse supertypes
	 */
	USE_UINT8_BIT_FIELD(guint, is_com_object   : 1);
	USE_UINT8_BIT_FIELD(guint, nested_classes_inited : 1); /* Whenever nested_class is initialized */
	USE_UINT8_BIT_FIELD(guint, interfaces_inited : 1); /* interfaces is initialized */
	USE_UINT8_BIT_FIELD(guint, simd_type       : 1); /* class is a simd intrinsic type */
	USE_UINT8_BIT_FIELD(guint, is_generic      : 1); /* class is a generic type definition */
	USE_UINT8_BIT_FIELD(guint, is_inflated     : 1); /* class is a generic instance */

	guint8     exception_type;	/* MONO_EXCEPTION_* */

	/* Additional information about the exception */
	/* Stored as property MONO_CLASS_PROP_EXCEPTION_DATA */
	//void       *exception_data;

	MonoClass* parent;
	MonoClass* nested_in;

	void* image;
	const char* name;
	const char* name_space;

	guint32    type_token;
	int        vtable_size; /* number of slots */

	guint16     interface_count;
	guint16     interface_id;        /* unique inderface id (for interfaces) */
	guint16     max_interface_id;

	guint16     interface_offsets_count;
	MonoClass** interfaces_packed;
	guint16* interface_offsets_packed;
	guint8* interface_bitmap;

	MonoClass** interfaces;

	union {
		int class_size; /* size of area for static fields */
		int element_size; /* for array types */
		int generic_param_token; /* for generic param types, both var and mvar */
	} sizes;

	/*
	 * From the TypeDef table
	 */
	guint32    flags;
	struct {
		guint32 first, count;
	} field, method;

	/* loaded on demand */
	void* marshal_info;

	/*
	 * Field information: Type and location from object base
	 */
	void* fields;

	MonoMethod** methods;

	/* used as the type of the this argument and when passing the arg by value */
	MonoType this_arg;
	MonoType byval_arg;

	void* generic_class;
	void* generic_container;

	void* reflection_info;

	void* gc_descr;

	void* runtime_info;

	/* next element in the class_cache hash list (in MonoImage) */
	MonoClass* next_class_cache;

	/* Generic vtable. Initialized by a call to mono_class_setup_vtable () */
	MonoMethod** vtable;

	/* Rarely used fields of classes */
	void* ext;

	void* user_data;
};

struct _MonoArrayType {
	MonoClass* eklass;
	guint8 rank;
	guint8 numsizes;
	guint8 numlobounds;
	int* sizes;
	int* lobounds;
};

struct _MonoMethodSignature {
	unsigned int  hasthis : 1;
	unsigned int  explicit_this : 1;
	unsigned int  call_convention : 6;
	unsigned int  pinvoke : 1;
	unsigned int  ref_count : 23;
	guint16       param_count;
	gint16        sentinelpos;
	unsigned int  generic_param_count : 30;
	unsigned int  is_inflated : 1;
	unsigned int  has_type_parameters : 1;
	MonoType* ret;
	MonoType* params[MONO_ZERO_LEN_ARRAY];
};

struct _MonoMethod {
	guint16 flags;  /* method flags */
	guint16 iflags; /* method implementation flags */
	guint32 token;
	MonoClass* klass;
	MonoMethodSignature* signature;
	/* name is useful mostly for debugging */
	const char* name;
	/* this is used by the inlining algorithm */
	unsigned int inline_info : 1;
	unsigned int inline_failure : 1;
	unsigned int wrapper_type : 5;
	unsigned int string_ctor : 1;
	unsigned int save_lmf : 1;
	unsigned int dynamic : 1; /* created & destroyed during runtime */
	unsigned int is_generic : 1; /* whenever this is a generic method definition */
	unsigned int is_inflated : 1; /* whether we're a MonoMethodInflated */
	unsigned int skip_visibility : 1; /* whenever to skip JIT visibility checks */
	unsigned int verification_success : 1; /* whether this method has been verified successfully.*/
	/* TODO we MUST get rid of this field, it's an ugly hack nobody is proud of. */
	unsigned int is_mb_open : 1;		/* This is the fully open instantiation of a generic method_builder. Worse than is_tb_open, but it's temporary */
	signed int slot : 17;

	/*
	 * If is_generic is TRUE, the generic_container is stored in image->property_hash,
	 * using the key MONO_METHOD_PROP_GENERIC_CONTAINER.
	 */
};