using System.Reflection;
using System.Runtime.InteropServices;
using ScreenshotTranslation.Core.Configuration;
using ScreenshotTranslation.Infrastructure.Windows;

namespace ScreenshotTranslation.Infrastructure.Tests.Windows;

public sealed class GlobalHotkeyServiceTests
{
    [Theory]
    [InlineData("NativeRegisterHotKey", "RegisterHotKey")]
    [InlineData("NativeUnregisterHotKey", "UnregisterHotKey")]
    public void Win32_imports_target_the_actual_user32_exports(
        string methodName,
        string expectedEntryPoint)
    {
        var nativeMethodsType = typeof(GlobalHotkeyService).GetNestedType(
            "Win32GlobalHotkeyNativeMethods",
            BindingFlags.NonPublic);
        var method = nativeMethodsType?.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);
        var import = method?.GetCustomAttribute<DllImportAttribute>();

        Assert.NotNull(import);
        Assert.Equal(expectedEntryPoint, import.EntryPoint);
    }

    [Fact]
    public Task Failed_previous_unregister_rolls_back_candidate_and_keeps_previous_registration()
    {
        return DispatcherTestHost.RunAsync(dispatcher =>
        {
            var nativeMethods = new FakeGlobalHotkeyNativeMethods();
            using var service = new GlobalHotkeyService(nativeMethods);
            var original = new HotkeyGesture(HotkeyModifiers.Control, 0x44);
            var replacement = new HotkeyGesture(HotkeyModifiers.Alt, 0x45);

            Assert.True(service.TryRegister(original).Succeeded);
            nativeMethods.UnregisterResults.Enqueue(false);
            nativeMethods.UnregisterResults.Enqueue(true);

            var result = service.TryRegister(replacement);

            Assert.False(result.Succeeded);
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
            Assert.Equal(2, nativeMethods.RegisterCalls.Count);
            Assert.Equal(nativeMethods.RegisterCalls[0].Identifier, nativeMethods.UnregisterCalls[0]);
            Assert.Equal(nativeMethods.RegisterCalls[1].Identifier, nativeMethods.UnregisterCalls[1]);

            Assert.True(service.TryRegister(original).Succeeded);
            Assert.Equal(2, nativeMethods.RegisterCalls.Count);
            return Task.CompletedTask;
        });
    }

    private sealed class FakeGlobalHotkeyNativeMethods : IGlobalHotkeyNativeMethods
    {
        public List<(int Identifier, uint Modifiers, uint VirtualKey)> RegisterCalls { get; } = [];

        public List<int> UnregisterCalls { get; } = [];

        public Queue<bool> UnregisterResults { get; } = [];

        public bool RegisterHotKey(
            nint windowHandle,
            int identifier,
            uint modifiers,
            uint virtualKey)
        {
            RegisterCalls.Add((identifier, modifiers, virtualKey));
            return true;
        }

        public bool UnregisterHotKey(nint windowHandle, int identifier)
        {
            UnregisterCalls.Add(identifier);
            return UnregisterResults.TryDequeue(out var result) ? result : true;
        }
    }
}
