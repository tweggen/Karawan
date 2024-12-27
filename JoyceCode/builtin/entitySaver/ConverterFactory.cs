using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using static engine.Logger;

namespace builtin.entitySaver;

public class ConverterFactory : JsonConverterFactory
{
     private readonly Context _context;
     private readonly ConverterRegistry _registry;
     
     
     public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
     {
          return _registry.CreateConverter(_context, typeToConvert, options);
     }


     public override bool CanConvert(Type typeToConvert)
     {
          return _registry.CanConvert(typeToConvert);
     }
     
     
     public ConverterFactory(ConverterRegistry registry, Context context)
     {
          _registry = registry;
          _context = context;
     }
}