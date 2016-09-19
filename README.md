# Shaman.BinSer
Binary serialization with support for cycles, delegates and events.

## Usage
```csharp
using Shaman.Runtime.Serialization;

var complexObject = /*...*/;
using (var serializer = new BinSerSerializer(outputStream))
{
    WriteObject(complexObject);
}

using (var deserializer = new BinSerDeserializer(inputStream))
{
    complexObject = deserializer.ReadObject<Something>();
}

```

## Features
* Serialization of lambdas/closures/`Func<>`, and their captured variables, with references to the actual implementation in the assembly.
* Serialization of references to `Type`, `MethodInfo`, `Assembly`, etc.
* Maintains the identity of objects, doesn't store the same object twice
* Interns strings

## Custom handlers
```csharp
BinSerCommon.SetUpCustomSerialization<Something>(
    (serializer, item) => { /*...*/ }
    (deserializer) => { /*return ...*/ }
)
```