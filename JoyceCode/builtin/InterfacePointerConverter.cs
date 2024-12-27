using System;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static engine.Logger;

namespace builtin;

public class InterfacePointerConverter<T> : JsonConverter<T> where T : class 
{
    private static readonly byte[] s_implementationAssemblyUtf8 = Encoding.UTF8.GetBytes("implementationAssembly");
    private static readonly byte[] s_implementationClassUtf8 = Encoding.UTF8.GetBytes("implementationClass");
    private static readonly byte[] s_implementationUtf8 = Encoding.UTF8.GetBytes("implementation");

    private bool _disableNext = false;

    public override T Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            ErrorThrow<InvalidCastException>($"Expected beginning of object, got {reader.TokenType}.");
        }

        reader.Read();

        bool haveAssembly = false;
        bool haveType = false;
        bool haveImplementation = false;
        Assembly? assemblyImplementation = null;
        Type? typeImplementation = null;
        string? strImplementationType = null;
        string? strImplementationAssembly = null;
        
        while (reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                ErrorThrow<InvalidCastException>($"Expected propertyname, got {reader.TokenType}.");
            }

            if (reader.ValueTextEquals(s_implementationAssemblyUtf8))
            {
                reader.Read(); 
                if (reader.TokenType != JsonTokenType.String)
                {
                    ErrorThrow<InvalidCastException>($"Expected string for assembly, got {reader.TokenType}.");
                }
                strImplementationAssembly = reader.GetString();
                assemblyImplementation = System.Reflection.Assembly.Load(strImplementationAssembly);
                if (null == assemblyImplementation)
                {
                    ErrorThrow<InvalidCastException>($"Unknown assembly {strImplementationAssembly}");
                }
                haveAssembly = true;
                reader.Read(); 
            } else if (reader.ValueTextEquals(s_implementationClassUtf8))
            {
                reader.Read();
                if (!haveAssembly)
                {
                    ErrorThrow<InvalidOperationException>($"No assembly defined before loading type.");
                }
                if (reader.TokenType != JsonTokenType.String)
                {
                    ErrorThrow<InvalidCastException>($"Expected string for type, got {reader.TokenType}.");
                }
                strImplementationType = reader.GetString();
                typeImplementation = assemblyImplementation.GetType(strImplementationType);
                if (null == typeImplementation)
                {
                    ErrorThrow<InvalidCastException>($"Unknown type {strImplementationType} in {strImplementationAssembly}");
                }
                haveType = true;
                reader.Read(); 
            } else if (reader.ValueTextEquals(s_implementationUtf8))
            {
                if (!haveType)
                {
                    ErrorThrow<InvalidOperationException>($"found implementation without previously defined type.");
                }

                reader.Read();
                if (reader.TokenType == JsonTokenType.Null)
                {
                    haveImplementation = true;
                    return null;
                }
                else
                {
                    object objImplementation = JsonSerializer.Deserialize(ref reader, typeImplementation, options);
                    reader.Read();
                    haveImplementation = true;
                    return objImplementation as T;
                }
                
            }
        }
        return null;
    }
    
    
    public override void Write(
        Utf8JsonWriter writer,
        T iBehavior,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (null != iBehavior)
        {
            Type type = iBehavior.GetType();
            writer.WriteString("implementationAssembly", type.Assembly.FullName);
            writer.WriteString("implementationClass", type.FullName);
            writer.WritePropertyName("implementation");
            writer.WriteRawValue(JsonSerializer.Serialize(
                iBehavior, type, options));
        }
        else
        {
            writer.WriteNull("implementation");
        }
        writer.WriteEndObject();
    }
}


