using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shaman.Runtime.Serialization
{
    [RestrictedAccess]
    public class BinSerDeserializer : IDisposable
    {
        private BinaryReader br;
        public BinSerDeserializer(BinaryReader reader)
        {
            Sanity.AssertFastReadByte(reader.BaseStream);
            br = reader;
            objects.Add(null);
            objects.Add(null);
            foreach (var item in BinSerCommon.WellKnownObjects)
            {
                objects.Add(item);
            }
#if CORECLR
            if (GetUninitializedObject == null)
            {
                GetUninitializedObject = ReflectionHelper.GetWrapper<Func<Type, object>>(typeof(string).Assembly(), "System.Runtime.Serialization.FormatterServices", "GetUninitializedObject");
            }
#endif
        }
#if CORECLR

        private Func<Type, object> GetUninitializedObject;
#endif

        private List<object> objects = new List<object>();

        public void Dispose()
        {
            objects = null;
        }

        private object ReadStruct(Type type)
        {
            if (type.IsEnum())
                return Enum.ToObject(type, ReadStructInternal(Enum.GetUnderlyingType(type)));
            return ReadStructInternal(type);
        }

        private object ReadStructInternal(Type type)
        {


            if (type == typeof(bool)) return br.Read7BitEncodedLong() != 0;
            else if (type == typeof(char)) return (char)br.Read7BitEncodedLong();

            else if (type == typeof(float)) return br.ReadSingle();
            else if (type == typeof(double)) return br.ReadDouble();
            else if (type == typeof(decimal)) return br.ReadDecimal();

            else if (type == typeof(sbyte)) return (sbyte)br.Read7BitEncodedLong();
            else if (type == typeof(byte)) return (byte)br.Read7BitEncodedLong();

            else if (type == typeof(short)) return (short)br.Read7BitEncodedLong();
            else if (type == typeof(ushort)) return (ushort)br.Read7BitEncodedLong();

            else if (type == typeof(int)) return (int)br.Read7BitEncodedLong();
            else if (type == typeof(uint)) return (uint)br.Read7BitEncodedLong();

            else if (type == typeof(long)) return (long)br.Read7BitEncodedLong();
            else if (type == typeof(ulong)) return br.ReadUInt64();

            var v = Activator.CreateInstance(type);



            foreach (var field in BinSerCommon.GetFields(type))
            {
                var fieldval = ReadObjectInternal(field.FieldType);
                field.SetValue(v, fieldval);
            }

            return v;



        }

        public T ReadObject<T>()
        {
            try
            {
                typeStack.Clear();
                return ReadObjectInternal<T>();

            }
            catch (Exception ex)
            {
                throw CreateDeserializationException(ex);
            }
        }

        private Exception CreateDeserializationException(Exception ex)
        {
            return new Exception("An error occurred while deserializing the object graph. Position: " + string.Join(" -> ", typeStack.Select(x => x.FullName).ToArray()), ex);
        }

        internal T ReadObjectInternal<T>()
        {
            return (T)ReadObjectInternal(typeof(T));
        }
        private List<Type> typeStack = new List<Type>();

        internal object ReadObjectInternal(Type expectedType)
        {
            typeStack.Add(expectedType);
            var q = ReadObjectInternalImpl(expectedType);
            typeStack.RemoveAt(typeStack.Count - 1);
            return q;

        }
        internal object ReadObjectInternalImpl(Type expectedType)
        {
            var expectedTypeInfo = expectedType.GetTypeInfo();
            if (expectedTypeInfo.IsValueType)
            {
                if (expectedTypeInfo.IsGenericType && expectedTypeInfo.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    if (br.Read7BitEncodedLong() == 1)
                    {
                        return ReadStruct(Nullable.GetUnderlyingType(expectedType));
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return ReadStruct(expectedType);
                }

            }
            else
            {
                var idx = (int)br.Read7BitEncodedLong();
                if (idx == 1)
                {
                    return null;
                }
                else if (idx != 0)
                {
                    return objects[idx];
                }
                else
                {
                    idx = objects.Count;
                    objects.Add(null);

                    var type = (Type)ReadObjectInternal(typeof(Type));
                    object obj;

                    if (type.Is<Delegate>())
                    {
                        var f = ReadObjectInternal<object>();
                        var invocationList = f as Delegate[];
                        if (invocationList != null)
                        {
                            obj = Delegate.Combine(invocationList);
                        }
                        else
                        {
                            var method = (MethodInfo)f;
                            var methodParamTypes = method.GetParameters().Select(x => x.ParameterType).ToList();
                            var target = ReadObjectInternal<object>();
                            var funcBase = type.IsGenericType() ? type.GetGenericTypeDefinition() : null;
                            var genargs = funcBase != null ? funcBase.GetGenericArguments() : null;
                            Type reducerType = null;
                            if (funcBase == typeof(Action) && methodParamTypes.Count == 1)
                            {
                                reducerType = typeof(SignatureReducerAction<>);
                            }
                            else if (funcBase == typeof(Action<>) && methodParamTypes.Count == 2)
                            {
                                reducerType = typeof(SignatureReducerAction<,>);
                            }
                            else if (funcBase == typeof(Action<,>) && methodParamTypes.Count == 3)
                            {
                                reducerType = typeof(SignatureReducerAction<,,>);
                            }
                            else if (funcBase == typeof(Func<>) && methodParamTypes.Count == 1)
                            {
                                methodParamTypes.Add(method.ReturnType);
                                reducerType = typeof(SignatureReducerFunc<,>);
                            }
                            else if (funcBase == typeof(Func<,>) && methodParamTypes.Count == 2)
                            {
                                methodParamTypes.Add(method.ReturnType);
                                reducerType = typeof(SignatureReducerFunc<,,>);
                            }
                            else if (funcBase == typeof(Func<,,>) && methodParamTypes.Count == 3)
                            {
                                methodParamTypes.Add(method.ReturnType);
                                reducerType = typeof(SignatureReducerFunc<,,,>);
                            }
                            if (reducerType != null)
                            {
                                var reducer = Activator.CreateInstance(reducerType.MakeGenericTypeFast(methodParamTypes.ToArray()), new object[] {  method });
                                Sanity.Assert(target == null);
                                var call = reducer.GetType().GetMethod("Call", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                return call.CreateDelegate(type, reducer);
                            }
                            try
                            {

                                if (target != null) obj = method.CreateDelegate(type, target);
                                else obj = method.CreateDelegate(type);
                            }
                            catch (Exception ex)
                            {
                                throw new Exception("Cannot create delegate of type " + type.FullName + " for method " + method.DeclaringType.FullName + "::" + method.Name + " - target: " + (target != null ? target.GetType().FullName : "(none)"), ex);
                            }
                        }
                    }
                    else if (type == typeof(string))
                    {
                        obj = br.ReadString();
                    }
                    else if (type == typeof(WeakReference))
                    {
                        var target = ReadObjectInternal<object>();
                        obj = new WeakReference(target);
                    }
                    else if (type.IsGenericType() && type.GetGenericTypeDefinition() == typeof(WeakReference<>))
                    {
                        var target = ReadObjectInternal<object>();
                        obj = Activator.CreateInstance(type, new object[] { target });
                    }
                    else if (type.IsArray)
                    {
                        var elType = type.GetElementType();
                        var length = br.Read7BitEncodedLong();
                        var arr = Array.CreateInstance(elType, (int)length);
                        obj = arr;
                        objects[idx] = obj;

                        for (int i = 0; i < length; i++)
                        {
                            arr.SetValue(ReadObjectInternal(elType), i);
                        }
                    }
                    else
                    {

                        var reader = BinSerCommon.customReaders.TryGetValue(type);
                        if (reader == null && type.IsGenericType()) reader = BinSerCommon.customReaders.TryGetValue(type.GetGenericTypeDefinition());
                        if (reader != null)
                        {
                            obj = reader(this, type);
                        }
                        else
                        {
#if CORECLR
                            obj = GetUninitializedObject(type);
#else
                            obj = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#endif
                            objects[idx] = obj;

                            foreach (var field in BinSerCommon.GetFields(type))
                            {
                                var val = ReadObjectInternal(field.FieldType);
                                field.SetValue(obj, val);
                            }
                        }
                    }
                    if (obj == null) throw new InvalidDataException("Deserialization of non-null value yielded to null value.");
                    objects[idx] = obj;
                    return obj;


                }
            }

        }

        private static T CreateDelegate<T>(MethodInfo method) where T : class
        {
            return (T)(object)method.CreateDelegate(typeof(T));
        }

        internal class SignatureReducerFunc<T1, TResult>
        {
            public SignatureReducerFunc(MethodInfo method)
            {
                Method = CreateDelegate<Func<T1, TResult>>(method);
            }
            public Func<T1, TResult> Method;
            public TResult Call()
            {
                return Method(default(T1));
            }
        }
        internal class SignatureReducerFunc<T1, T2, TResult>
        {
            public SignatureReducerFunc(MethodInfo method)
            {
                Method = CreateDelegate<Func<T1, T2, TResult>>(method);
            }
            public Func<T1, T2, TResult> Method;
            public TResult Call(T2 arg2)
            {
                return Method(default(T1), arg2);
            }
        }
        internal class SignatureReducerFunc<T1, T2, T3, TResult>
        {
            public SignatureReducerFunc(MethodInfo method)
            {
                Method = CreateDelegate<Func<T1, T2, T3, TResult>>(method);
            }
            public Func<T1, T2, T3, TResult> Method;
            public TResult Call(T2 arg2, T3 arg3)
            {
                return Method(default(T1), arg2, arg3);
            }
        }
        internal class SignatureReducerAction<T1>
        {
            public SignatureReducerAction(MethodInfo method)
            {
                Method = CreateDelegate<Action<T1>>(method);
            }
            public Action<T1> Method;
            public void Call()
            {
                Method(default(T1));
            }
        }
        internal class SignatureReducerAction<T1, T2>
        {
            public SignatureReducerAction(MethodInfo method)
            {
                Method = CreateDelegate<Action<T1, T2>>(method);
            }
            public Action<T1, T2> Method;
            public void Call(T2 arg2)
            {
                Method(default(T1), arg2);
            }
        }
        internal class SignatureReducerAction<T1, T2, T3>
        {
            public SignatureReducerAction(MethodInfo method)
            {
                Method = CreateDelegate<Action<T1, T2, T3>>(method);
            }
            public Action<T1, T2, T3> Method;
            public void Call(T2 arg2, T3 arg3)
            {
                Method(default(T1), arg2, arg3);
            }
        }

        public void ReadObjectInline(object obj)
        {
            typeStack.Clear();
            try
            {


                var zero = br.Read7BitEncodedLong();
                if (zero != 0) throw new InvalidDataException();
                var idx = objects.Count;
                objects.Add(obj);

                var type = (Type)ReadObjectInternal(typeof(Type));
                foreach (var field in BinSerCommon.GetFields(type))
                {
                    var val = ReadObjectInternal(field.FieldType);
                    field.SetValue(obj, val);
                }
            }
            catch (Exception ex)
            {
                throw CreateDeserializationException(ex);
            }
        }

        public void SetupObjectReplacement(object obj)
        {
            objects.Add(obj);
        }

        public static T ReadFile<T>(string path)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            using (var br = new BinaryReader(stream, Encoding.UTF8))
            using (var des = new BinSerDeserializer(br))
            {
                return (T)des.ReadObject<object>();
            }
        }
    }
}
