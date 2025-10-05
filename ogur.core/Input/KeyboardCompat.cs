using System.Threading;
using System.Threading.Tasks;
using Ogur.Abstractions.Input;

namespace Ogur.Core.Input;

/// <summary>
/// Compatibility facade exposing PressKey and PressKey2 methods in the requested form.
/// </summary>
public sealed class KeyboardCompat
{
    private readonly IKeyboardSynthesizer _synth;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyboardCompat"/> class.
    /// </summary>
    /// <param name="synth">Keyboard synthesizer.</param>
    public KeyboardCompat(IKeyboardSynthesizer synth) => _synth = synth;

    /// <summary>
    /// Simulates a key press (down+up) using a scan code.
    /// </summary>
    /// <param name="btn">Scan code value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task.</returns>
    public Task PressKey(ScanCode btn, CancellationToken ct = default) =>
        _synth.PressKeyAsync(btn, ct);

    /// <summary>
    /// Simulates a key press using two discrete events (down then up).
    /// </summary>
    /// <param name="btn">Scan code value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task.</returns>
    public Task PressKey2(ScanCode btn, CancellationToken ct = default) =>
        _synth.PressKey2Async(btn, ct);
}