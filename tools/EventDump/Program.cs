using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Linq;
using System.Reflection;

foreach (var t in typeof(IFocuserConsumer).GetInterfaces().Concat(new[] { typeof(IFocuserConsumer) })) {
    Console.WriteLine($"=== {t.FullName} ===");
    foreach (var m in t.GetMethods()) Console.WriteLine($"  {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
}

var method = typeof(IFocuserConsumer).GetMethod("NewAutoFocusPoint");
Console.WriteLine($"NewAutoFocusPoint param: {method?.GetParameters()[0].ParameterType.FullName}");