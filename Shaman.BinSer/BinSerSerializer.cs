using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BinaryWriter = System.IO.BinaryWriter;

namespace Shaman.Runtime.Serialization
{
    [RestrictedAccess]
    public class BinSerSerializer : IDisposable
    {

        private bool leaveOpen;

        public BinSerSerializer(Stream stream) : this(new BinaryWriter(stream, Encoding.UTF8),  false)
        {
        }
        public BinSerSerializer(Stream stream, bool leaveOpen) : this(new BinaryWriter(stream, Encoding.UTF8, leaveOpen), leaveOpen)
        {

        }
        public BinSerSerializer(BinaryWriter writer) : this(writer, false)
        {
        }
        public BinSerSerializer(BinaryWriter writer, bool leaveOpen)
        {
            this.bw = writer;
            ++lastObjectId; // null
            foreach (var item in BinSerCommon.WellKnownObjects)
            {
                lastObjectId++;
                if (item != null)
                    objects[item] = lastObjectId;
            }
            this.leaveOpen = leaveOpen;
        }


        [StaticFieldCategory(StaticFieldCategory.Stable)]
        private readonly static MethodInfo writeObjMethod = typeof(BinSerSerializer).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).First(x => x.Name == "WriteObjectInternal" && x.GetParameters().Length == 2);


        private Dictionary<object, int> objects = new Dictionary<object, int>(new ObjectReferenceEqualityComparer());
        private BinaryWriter bw;
        private int lastObjectId;

        public void Dispose()
        {
            this.objects = null;
            if (!leaveOpen)
                bw.Dispose();
        }

        private void WriteStructInternal(object value)
        {
            Sanity.Assert(!(value is IntPtr));
            Sanity.Assert(!(value is UIntPtr));

            var type = value.GetType();
            if (value is bool) bw.Write7BitEncodedLong((bool)value ? 1 : 0);
            else if (value is char) bw.Write7BitEncodedLong((char)value);

            else if (value is double) bw.Write((double)value);
            else if (value is float) bw.Write((float)value);
            else if (value is decimal) bw.Write((decimal)value);

            else if (value is sbyte) bw.Write7BitEncodedLong((sbyte)value);
            else if (value is byte) bw.Write7BitEncodedLong((byte)value);

            else if (value is short) bw.Write7BitEncodedLong((short)value);
            else if (value is ushort) bw.Write7BitEncodedLong((ushort)value);

            else if (value is int) bw.Write7BitEncodedLong((int)value);
            else if (value is uint) bw.Write7BitEncodedLong((uint)value);

            else if (value is long) bw.Write7BitEncodedLong((long)value);
            else if (value is ulong) bw.Write((ulong)value);
            else if (type.IsEnum())
            {
                var bak = Enum.GetUnderlyingType(type);
                if (bak == typeof(ulong)) bw.Write((ulong)Convert.ChangeType(value, typeof(ulong)));
                else bw.Write7BitEncodedLong((long)Convert.ChangeType(value, typeof(long)));
            }
            else
            {
                AddToObjectStack(value);
                

                if (BinSerCommon.Configuration_EmitSerializer)
                {
                    var w = GetWriteEachField(type);
                    w(this, value);
                }
                else
                {
                    foreach (var field in BinSerCommon.GetFields(type))
                    {
                        var v = field.GetValue(value);
                        WriteObjectInternal(v, field.FieldType);
                    }
                }

                objectStack.RemoveAt(objectStack.Count - 1);




            }
        }


        public void SetupReplaceableObject(object obj)
        {
            objects[obj] = ++lastObjectId;
        }

        internal void WriteObjectInternal<T>(T obj) where T : class
        {
            WriteObjectInternal(obj, typeof(T));
        }

        public void WriteObject<T>(T obj) where T : class
        {
            objectStack.Clear();
            try
            {
                WriteObjectInternal(obj, typeof(object));
            }
            catch (Exception ex)
            {

                throw new Exception("Cannot serialize object graph: " + string.Join(" -> ", objectStack.Select(x => x.GetType().FullName)), ex);
            }
        }
        private List<object> objectStack = new List<object>();

        [StaticFieldCategory(StaticFieldCategory.Stable)]
        private readonly static Type PointerType = typeof(IntPtr).Assembly().GetType("System.Reflection.Pointer", true, false);

        internal void WriteObjectInternal(object obj, Type expectedType)
        {
            Sanity.Assert(obj == null || obj.GetType() != PointerType);

            var type = obj != null ? obj.GetType() : null;
            if (obj != null && !expectedType.IsValueType())
            {

                int idx;
                if (objects.TryGetValue(obj, out idx))
                {
                    bw.Write7BitEncodedLong(idx);
                }
                else
                {
                    if (Configuration_WriteObjectListDuringSerialization)
                    {
                        try
                        {
                            Console.WriteLine(obj.ToString());
                        }
                        catch (Exception)
                        {
                        }
                    }
                    bw.Write7BitEncodedLong(0);

                    lastObjectId++;
                    objects[obj] = lastObjectId;

                    WriteObjectInternal(type, typeof(Type));
                    AddToObjectStack(obj);

                    if (type.IsArray)
                    {
                        var arr = (Array)obj;
                        bw.Write7BitEncodedLong(arr.Length);
                        var elemType = type.GetElementType();
                        for (int i = 0; i < arr.Length; i++)
                        {
                            WriteObjectInternal(arr.GetValue(i), elemType);
                        }
                    }
                    else if (type == typeof(string))
                    {
                        bw.Write((string)obj);
                    }
                    else if (obj is Delegate)
                    {
                        var deleg = (MulticastDelegate)obj;
                        var ilist = deleg.GetInvocationList();
                        if (ilist != null && (ilist.Length != 1 || deleg != ilist[0]))
                        {
                            WriteObjectInternal(ilist);
                        }
                        else
                        {

                            WriteObjectInternal(deleg.GetMethodInfo());
                            WriteObjectInternal(deleg.Target, typeof(object));

                        }
                    }
                    else if (type == typeof(WeakReference))
                    {
                        WriteObjectInternal(((WeakReference)obj).Target);
                    }
                    else if (type.IsGenericType() && type.GetGenericTypeDefinition() == typeof(WeakReference<>))
                    {
                        var method = type.GetMethod("TryGetTarget");
                        var args = new object[1];
                        method.Invoke(obj, args);
                        WriteObjectInternal(args[0]);
                    }
                    else
                    {
                        var writer = BinSerCommon.customWriters.TryGetValue(type);
                        if (writer == null && type.IsGenericType()) writer = BinSerCommon.customWriters.TryGetValue(type.GetGenericTypeDefinition());
                        if (writer != null)
                        {
                            writer(this, obj);
                        }
                        else
                        {
                            if (obj is Attribute)
                            {
                                throw new ArgumentException("Serialization of Attribute objects is disallowed.");
                            }

                            var ser = obj as IHasBinSerCallbacks;
                            if (ser != null) ser.OnSerializing();

                            //if (type.Is<Task>()) Debugger.Break();

                            if (BinSerCommon.Configuration_EmitSerializer)
                            {
                                var w = GetWriteEachField(type);
                                w(this, obj);
                            }
                            else
                            {
                                foreach (var field in BinSerCommon.GetFields(type))
                                {
                                    var v = field.GetValue(obj);
                                    WriteObjectInternal(v, field.FieldType);
                                }
                            }

                        }
                    }

                    objectStack.RemoveAt(objectStack.Count - 1);
                }

            }
            else if (expectedType.IsGenericType() && expectedType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (obj != null)
                {
                    bw.Write7BitEncodedLong(1);
                    WriteStructInternal(obj);
                }
                else
                {
                    bw.Write7BitEncodedLong(0);
                }
            }
            else if (expectedType.IsValueType())
            {
                WriteStructInternal(obj);
            }
            else if (obj == null)
            {
                bw.Write7BitEncodedLong(1);
            }
            else
            {
                Sanity.ShouldntHaveHappened();
            }







        }

        private bool hasLoggedSuspiciousDepth;

        private void AddToObjectStack(object obj)
        {
            objectStack.Add(obj);
            if (objectStack.Count >= 400 && !hasLoggedSuspiciousDepth)
            {
                hasLoggedSuspiciousDepth = true;
                Sanity.ShouldntHaveHappenedButTryToContinue(new Exception("BinSerSerializer stack is becoming very deep:" + string.Join(" -> ", objectStack.Select(x => x.GetType().FullName))));
            }
        }



        


        private Action<BinSerSerializer, object> GetWriteEachField(Type type)
        {

            var q = _writeEachFieldCache.TryGetValue(type);
            if (q == null)
            {

                /*
                var assemblyName = new AssemblyName("An.Assembly");
                var appDomain = AppDomain.CurrentDomain;
                var assemblyBuilder = appDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
                var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, "tonydinozzo.dll");
                var typeBuilder = moduleBuilder.DefineType("MyClass", TypeAttributes.Public | TypeAttributes.Class);
                var method = typeBuilder.DefineMethod("Demow", MethodAttributes.Public | MethodAttributes.Static, (Type)null, new[] { typeof(BinSerSerializer), typeof(object) });
                */
                var method = new DynamicMethod("WriteEachField", null, new[] { typeof(BinSerSerializer), typeof(object) }, true);
                var gen = method.GetILGenerator();
                gen.DeclareLocal(type);



                gen.Emit(OpCodes.Ldarg_1);
                if (type.IsValueType()) gen.Emit(OpCodes.Unbox_Any, type);
                else gen.Emit(OpCodes.Castclass, type);
                gen.Emit(OpCodes.Stloc_0);


                foreach (var field in BinSerCommon.GetFields(type))
                {
                    gen.Emit(OpCodes.Ldarg_0);
                    gen.Emit(OpCodes.Ldloc_0);
                    gen.Emit(OpCodes.Ldfld, field);
                    if (field.FieldType.IsValueType())
                    {
                        gen.Emit(OpCodes.Box, field.FieldType);
                    }
                    gen.Emit(OpCodes.Ldtoken, field.FieldType);
                    gen.Emit(OpCodes.Call, BinSerCommon.GetTypeFromHandleMethod);
                    gen.Emit(OpCodes.Callvirt, writeObjMethod);
                }
                gen.Emit(OpCodes.Ret);
                q = (Action<BinSerSerializer, object>)method.CreateDelegate(typeof(Action<BinSerSerializer, object>));

                /*  var t = typeBuilder.CreateType();
                  assemblyBuilder.Save("tonydinozzo.dll");
                  */
                _writeEachFieldCache.TryAdd(type, q);
            }

            return q;




        }


        [StaticFieldCategory(StaticFieldCategory.Cache)]
        private static ConcurrentDictionary<Type, Action<BinSerSerializer, object>> _writeEachFieldCache = new ConcurrentDictionary<Type, Action<BinSerSerializer, object>>();

        [Configuration]
        public static readonly bool Configuration_WriteObjectListDuringSerialization = false;

        public static void WriteFile<T>(string path, T obj)
        {
            var temp = MaskedFile.GetMaskedPathFromFile(path);
            try
            {
                using (var fileStream = File.Open(temp, FileMode.Create, FileAccess.Write, FileShare.Delete))
                using (var bw = new BinaryWriter(fileStream, Encoding.UTF8))
                using (var ser = new BinSerSerializer(bw))
                {
                    ser.WriteObject<object>(obj);
                }
                File.Delete(path);
                MaskedFile.PublishMaskedFile(temp, path);
            }
            finally
            {
                MaskedFile.TryDeleteTempFile(temp);
            }
        }



    }
}
