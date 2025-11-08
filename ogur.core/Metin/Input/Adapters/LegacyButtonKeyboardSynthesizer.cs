using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ogur.Abstractions.Input;
using Ogur.Core.Metin.Legacy;

namespace Ogur.Core.Metin.Input.Adapters;

/// <summary>
/// Bridges the legacy <c>HACK.Button</c> static API to the clean <see cref="IKeyboardSynthesizer"/> contract.
/// </summary>
public sealed class LegacyButtonKeyboardSynthesizer : IKeyboardSynthesizer
{
    private readonly ILogger<LegacyButtonKeyboardSynthesizer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyButtonKeyboardSynthesizer"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public LegacyButtonKeyboardSynthesizer(ILogger<LegacyButtonKeyboardSynthesizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Presses a key (down+up) using legacy <c>HACK.Button.PressKey</c>.
    /// </summary>
    /// <param name="code">Scan code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task.</returns>
    public Task PressKeyAsync(ScanCode code, CancellationToken ct)
    {
        Button.PressKey((Button.BT7)(short)code);
        _logger.LogDebug("Legacy PressKey({Code})", code);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Presses a key using two discrete events via legacy <c>HACK.Button.PressKey2</c>.
    /// </summary>
    /// <param name="code">Scan code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task.</returns>
    public Task PressKey2Async(ScanCode code, CancellationToken ct)
    {
        Button.PressKey2((Button.BT7)(short)code);
        _logger.LogDebug("Legacy PressKey2({Code})", code);
        return Task.CompletedTask;
    }
}