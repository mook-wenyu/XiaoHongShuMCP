using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 涓枃锛氶粯璁ゆ嫙浜哄寲鍔ㄤ綔鎵ц鍣紝瀹炵幇榧犳爣銆侀敭鐩樸€佹粴鍔ㄧ瓑琛屼负鐨勯殢鏈哄寲銆?/// English: Default executor that performs human-like mouse, keyboard and scrolling actions with randomisation.
/// </summary>
public sealed class HumanizedInteractionExecutor : IHumanizedInteractionExecutor
{
    private readonly IInteractionLocatorBuilder _locatorBuilder;
    private readonly HumanBehaviorOptions _behaviorOptions;
    private readonly ILogger<HumanizedInteractionExecutor> _logger;
    private readonly ConditionalWeakTable<IPage, MouseState> _mouseStates = new();
    private readonly ConcurrentDictionary<string, Random> _profileRandoms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _baseRandom;
    private readonly object _randomLock = new();

    public HumanizedInteractionExecutor(
        IInteractionLocatorBuilder locatorBuilder,
        IOptions<HumanBehaviorOptions> behaviorOptions,
        ILogger<HumanizedInteractionExecutor> logger,
        Random? random = null)
    {
        _locatorBuilder = locatorBuilder ?? throw new ArgumentNullException(nameof(locatorBuilder));
        _behaviorOptions = behaviorOptions?.Value ?? throw new ArgumentNullException(nameof(behaviorOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseRandom = random ?? Random.Shared;
    }

    public async Task ExecuteAsync(IPage page, HumanizedActionScript script, CancellationToken cancellationToken = default)
    {
        if (script is null)
        {
            throw new ArgumentNullException(nameof(script));
        }

        foreach (var action in script.Actions)
        {
            await ExecuteAsync(page, action, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ExecuteAsync(IPage page, HumanizedAction action, CancellationToken cancellationToken = default)
    {
        if (page is null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var profile = ResolveProfile(action.BehaviorProfile);
        var random = GetRandom(action.BehaviorProfile, profile);

        if (action.Timing.DelayBefore > TimeSpan.Zero)
        {
            await Task.Delay(action.Timing.DelayBefore, cancellationToken).ConfigureAwait(false);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(action.Timing.Timeout);
        var timeoutToken = timeoutCts.Token;

        switch (action.Type)
        {
            case HumanizedActionType.Hover:
                await PerformHoverAsync(page, action, profile, random, timeoutToken).ConfigureAwait(false);
                break;
            case HumanizedActionType.Click:
                await PerformClickAsync(page, action, profile, random, timeoutToken).ConfigureAwait(false);
                break;
            case HumanizedActionType.MoveRandom:
                await PerformRandomMoveAsync(page, profile, random, timeoutToken).ConfigureAwait(false);
                break;
            case HumanizedActionType.Wheel:
                await PerformWheelAsync(page, action, profile, random, timeoutToken).ConfigureAwait(false);
                break;
            case HumanizedActionType.ScrollTo:
                await PerformScrollToAsync(page, action, profile, random, timeoutToken).ConfigureAwait(false);
                break;
            case HumanizedActionType.InputText:
                await PerformInputTextAsync(page, action, profile, random, timeoutToken).ConfigureAwait(false);
                break;
            case HumanizedActionType.PressKey:
                await PerformPressKeyAsync(page, action, profile, random, timeoutToken).ConfigureAwait(false);
                break;
            case HumanizedActionType.Hotkey:
                await PerformHotkeyAsync(page, action, profile, random, timeoutToken).ConfigureAwait(false);
                break;
            case HumanizedActionType.WaitFor:
                await PerformWaitForAsync(page, action, timeoutToken).ConfigureAwait(false);
                break;
            case HumanizedActionType.UploadFile:
                await PerformUploadFileAsync(page, action, timeoutToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException($"动作类型 {action.Type} 尚未实现。");
        }

        if (action.Timing.DelayAfter > TimeSpan.Zero)
        {
            await Task.Delay(action.Timing.DelayAfter, cancellationToken).ConfigureAwait(false);
        }

        await MaybeApplyIdlePauseAsync(action, cancellationToken).ConfigureAwait(false);
        await MaybeRandomIdleAsync(profile, random, cancellationToken).ConfigureAwait(false);
    }

    private async Task PerformHoverAsync(IPage page, HumanizedAction action, HumanBehaviorProfileOptions profile, Random random, CancellationToken cancellationToken)
    {
        var locator = await ResolveLocatorAsync(page, action, cancellationToken).ConfigureAwait(false);
        var clickStrategy = ResolveClickStrategy(action, profile);
        await MoveMouseAsync(page, locator, profile, clickStrategy, random, cancellationToken, focus: false).ConfigureAwait(false);
    }

    private async Task PerformClickAsync(IPage page, HumanizedAction action, HumanBehaviorProfileOptions profile, Random random, CancellationToken cancellationToken)
    {
        var locator = await ResolveLocatorAsync(page, action, cancellationToken).ConfigureAwait(false);
        var clickStrategy = ResolveClickStrategy(action, profile);
        var target = await MoveMouseAsync(page, locator, profile, clickStrategy, random, cancellationToken, focus: true).ConfigureAwait(false);

        var clickOptions = new LocatorClickOptions
        {
            Position = new Position
            {
                X = (float)target.Relative.X,
                Y = (float)target.Relative.Y
            },
            Timeout = (float)Math.Max(500d, action.Timing.Timeout.TotalMilliseconds)
        };

        await locator.ClickAsync(clickOptions).ConfigureAwait(false);
    }

    private async Task PerformRandomMoveAsync(IPage page, HumanBehaviorProfileOptions profile, Random random, CancellationToken cancellationToken)
    {
        var viewport = page.ViewportSize;
        var width = viewport?.Width ?? 1280;
        var height = viewport?.Height ?? 720;

        var x = random.NextDouble() * width;
        var y = random.NextDouble() * height;

        var state = GetMouseState(page);
        var start = state.Position ?? new Point(x, y);
        var steps = SampleInt(profile.MouseMoveSteps, random);
        var path = BuildMousePath(start, new Point(x, y), steps, profile.UseCurvedPaths, random);

        foreach (var point in path)
        {
            await page.Mouse.MoveAsync((float)point.X, (float)point.Y, new MouseMoveOptions { Steps = 1 }).ConfigureAwait(false);
        }

        state.Position = new Point(x, y);
    }

    private async Task PerformWheelAsync(IPage page, HumanizedAction action, HumanBehaviorProfileOptions profile, Random random, CancellationToken cancellationToken)
    {
        var deltaX = action.Parameters.WheelDeltaX ?? 0d;
        var deltaY = action.Parameters.WheelDeltaY;

        if (!deltaY.HasValue)
        {
            var (min, max) = profile.WheelDelta.Normalize();
            deltaY = min + (random.NextDouble() * (max - min));
            if (random.NextDouble() < profile.ReverseScrollProbability)
            {
                deltaY *= -1;
            }
        }

        await page.Mouse.WheelAsync((float)deltaX, (float)deltaY.Value).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(random.Next(40, 120)), cancellationToken).ConfigureAwait(false);
    }

    private async Task PerformScrollToAsync(IPage page, HumanizedAction action, HumanBehaviorProfileOptions profile, Random random, CancellationToken cancellationToken)
    {
        var locator = await ResolveLocatorAsync(page, action, cancellationToken).ConfigureAwait(false);
        await locator.ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions
        {
            Timeout = (float)Math.Max(500d, action.Timing.Timeout.TotalMilliseconds)
        }).ConfigureAwait(false);

        if (profile.HesitationProbability > 0 && random.NextDouble() < profile.HesitationProbability)
        {
            await PerformWheelAsync(page, action, profile, random, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PerformInputTextAsync(IPage page, HumanizedAction action, HumanBehaviorProfileOptions profile, Random random, CancellationToken cancellationToken)
    {
        var locator = await ResolveLocatorAsync(page, action, cancellationToken).ConfigureAwait(false);
        var text = action.Parameters.Text ?? action.Parameters.SecondaryText;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("InputText 鍔ㄤ綔闇€瑕佹彁渚?text 鎴?secondaryText 鍙傛暟銆?");
        }

        var clickStrategy = ResolveClickStrategy(action, profile);
        var target = await MoveMouseAsync(page, locator, profile, clickStrategy, random, cancellationToken, focus: true).ConfigureAwait(false);
        await locator.ClickAsync(new LocatorClickOptions
        {
            Position = new Position { X = (float)target.Relative.X, Y = (float)target.Relative.Y },
            Timeout = (float)Math.Max(500d, action.Timing.Timeout.TotalMilliseconds)
        }).ConfigureAwait(false);

        await locator.FillAsync(string.Empty, new LocatorFillOptions
        {
            Timeout = (float)Math.Max(500d, action.Timing.Timeout.TotalMilliseconds)
        }).ConfigureAwait(false);

        foreach (var ch in text)
        {
            var delay = SampleDelay(profile.TypingInterval, random);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

            if (profile.ErrorCorrectionProbability > 0 && random.NextDouble() < profile.ErrorCorrectionProbability)
            {
                var mistake = GenerateTypo(random, ch);
                await page.Keyboard.TypeAsync(mistake.ToString(CultureInfo.InvariantCulture));
                var correctionDelay = SampleDelay(profile.ErrorCorrectionDelay, random);
                await Task.Delay(correctionDelay, cancellationToken).ConfigureAwait(false);
                await page.Keyboard.PressAsync("Backspace");
            }

            await page.Keyboard.TypeAsync(ch.ToString(CultureInfo.InvariantCulture));
        }
    }

    private async Task PerformPressKeyAsync(IPage page, HumanizedAction action, HumanBehaviorProfileOptions profile, Random random, CancellationToken cancellationToken)
    {
        var key = action.Parameters.Text;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("PressKey 鍔ㄤ綔闇€瑕佹彁渚?text 鍙傛暟浣滀负鎸夐敭銆?");
        }

        var delay = SampleDelay(profile.TypingInterval, random);
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        await page.Keyboard.PressAsync(key.Trim());
    }

    private async Task PerformHotkeyAsync(IPage page, HumanizedAction action, HumanBehaviorProfileOptions profile, Random random, CancellationToken cancellationToken)
    {
        if (action.Parameters.Hotkeys.Count == 0)
        {
            throw new InvalidOperationException("Hotkey 鍔ㄤ綔闇€瑕佹彁渚涜嚦灏戜竴涓?hotkeys 鍊笺€?");
        }

        foreach (var hotkey in action.Parameters.Hotkeys)
        {
            if (string.IsNullOrWhiteSpace(hotkey))
            {
                continue;
            }

            var delay = SampleDelay(profile.HotkeyInterval, random);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            await page.Keyboard.PressAsync(hotkey.Trim());
        }
    }

    private async Task PerformSelectOptionAsync(IPage page, HumanizedAction action, CancellationToken cancellationToken)
    {
        var locator = await ResolveLocatorAsync(page, action, cancellationToken).ConfigureAwait(false);
        var value = action.Parameters.Text;
        var label = action.Parameters.SecondaryText;

        if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(label))
        {
            throw new InvalidOperationException("SelectOption 鍔ㄤ綔闇€瑕?text锛坴alue锛夋垨 secondaryText锛坙abel锛夈€?");
        }

        var options = new SelectOptionValue
        {
            Value = value,
            Label = label
        };

        await locator.SelectOptionAsync(options, new LocatorSelectOptionOptions
        {
            Timeout = (float)Math.Max(500d, action.Timing.Timeout.TotalMilliseconds)
        }).ConfigureAwait(false);
    }

    private async Task PerformUploadFileAsync(IPage page, HumanizedAction action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.Parameters.FilePath))
        {
            throw new InvalidOperationException("UploadFile 鍔ㄤ綔闇€瑕佹彁渚?filePath 鍙傛暟銆?");
        }

        var locator = await ResolveLocatorAsync(page, action, cancellationToken).ConfigureAwait(false);
        await locator.SetInputFilesAsync(action.Parameters.FilePath, new LocatorSetInputFilesOptions
        {
            Timeout = (float)Math.Max(500d, action.Timing.Timeout.TotalMilliseconds)
        }).ConfigureAwait(false);
    }

    private static Task PerformIdlePauseAsync(HumanizedAction action, CancellationToken cancellationToken)
        => action.Timing.IdlePause > TimeSpan.Zero
            ? Task.Delay(action.Timing.IdlePause, cancellationToken)
            : Task.CompletedTask;

    private async Task PerformWaitForAsync(IPage page, HumanizedAction action, CancellationToken cancellationToken)
    {
        if (action.Target is not null && !action.Target.IsEmpty())
        {
            var locator = await _locatorBuilder.ResolveAsync(page, action.Target, cancellationToken).ConfigureAwait(false);
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = (float)Math.Max(500d, action.Timing.Timeout.TotalMilliseconds)
            }).ConfigureAwait(false);
        }
        else
        {
            await Task.Delay(action.Timing.Timeout, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PerformDragAsync(IPage page, HumanizedAction action, HumanBehaviorProfileOptions profile, Random random, CancellationToken cancellationToken)
    {
        var source = await ResolveLocatorAsync(page, action, cancellationToken).ConfigureAwait(false);
        var dragStrategy = ResolveClickStrategy(action, profile);
        var start = await MoveMouseAsync(page, source, profile, dragStrategy, random, cancellationToken, focus: false).ConfigureAwait(false);

        var deltaX = action.Parameters.ScrollDelta ?? action.Parameters.WheelDeltaX ?? random.NextDouble() * 120 - 60;
        var deltaY = action.Parameters.WheelDeltaY ?? random.NextDouble() * 120 - 60;

        await page.Mouse.DownAsync(new MouseDownOptions { Button = MouseButton.Left });

        var state = GetMouseState(page);
        var startPoint = state.Position ?? start.Absolute;
        var targetPoint = new Point(startPoint.X + deltaX, startPoint.Y + deltaY);
        var steps = SampleInt(profile.MouseMoveSteps, random);
        var path = BuildMousePath(startPoint, targetPoint, steps, profile.UseCurvedPaths, random);

        foreach (var point in path)
        {
            await page.Mouse.MoveAsync((float)point.X, (float)point.Y, new MouseMoveOptions { Steps = 1 }).ConfigureAwait(false);
        }

        await Task.Delay(SampleDelay(profile.IdlePause, random), cancellationToken).ConfigureAwait(false);
        await page.Mouse.UpAsync(new MouseUpOptions { Button = MouseButton.Left });
        state.Position = targetPoint;
    }

    private async Task PerformDragAndDropAsync(IPage page, HumanizedAction action, HumanBehaviorProfileOptions profile, Random random, CancellationToken cancellationToken)
    {
        if (action.Parameters.SecondaryTarget is null)
        {
            throw new InvalidOperationException("DragAndDrop 鍔ㄤ綔闇€瑕?secondaryTarget 鍙傛暟銆?");
        }

        var source = await ResolveLocatorAsync(page, action, cancellationToken).ConfigureAwait(false);
        var dragStrategy = ResolveClickStrategy(action, profile);
        var targetLocator = await _locatorBuilder.ResolveAsync(page, action.Parameters.SecondaryTarget, cancellationToken).ConfigureAwait(false);

        await targetLocator.ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions
        {
            Timeout = (float)Math.Max(500d, action.Timing.Timeout.TotalMilliseconds)
        }).ConfigureAwait(false);

        var start = await MoveMouseAsync(page, source, profile, dragStrategy, random, cancellationToken, focus: false).ConfigureAwait(false);
        await page.Mouse.DownAsync(new MouseDownOptions { Button = MouseButton.Left });

        var targetBox = await EnsureBoundingBoxAsync(targetLocator).ConfigureAwait(false);
        var destPoint = PickPointInBox(targetBox, dragStrategy.Jitter, dragStrategy.EdgeProbability, random);

        var state = GetMouseState(page);
        var startPoint = state.Position ?? start.Absolute;
        var steps = SampleInt(profile.MouseMoveSteps, random);
        var path = BuildMousePath(startPoint, destPoint, steps, profile.UseCurvedPaths, random);

        foreach (var point in path)
        {
            await page.Mouse.MoveAsync((float)point.X, (float)point.Y, new MouseMoveOptions { Steps = 1 }).ConfigureAwait(false);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(random.Next(40, 120)), cancellationToken).ConfigureAwait(false);
        await page.Mouse.UpAsync(new MouseUpOptions { Button = MouseButton.Left });
        state.Position = destPoint;
    }

    private async Task PerformEvaluateAsync(IPage page, HumanizedAction action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.Parameters.Text))
        {
            throw new InvalidOperationException("Evaluate/Custom 鍔ㄤ綔闇€瑕?text 瀛楁浣滀负鑴氭湰銆?");
        }

        await page.EvaluateAsync(action.Parameters.Text!, action.Parameters.Metadata);
    }

    private async Task<LocatorTarget> MoveMouseAsync(IPage page, ILocator locator, HumanBehaviorProfileOptions profile, ClickStrategy clickStrategy, Random random, CancellationToken cancellationToken, bool focus)
    {
        await locator.ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions
        {
            Timeout = 5_000
        }).ConfigureAwait(false);

        var box = await EnsureBoundingBoxAsync(locator).ConfigureAwait(false);
        var destination = PickPointInBox(box, clickStrategy.Jitter, clickStrategy.EdgeProbability, random);
        var relative = new Point(destination.X - box.X, destination.Y - box.Y);

        var state = GetMouseState(page);
        var start = state.Position ?? PickRandomEntryPoint(destination, random);
        var steps = SampleInt(profile.MouseMoveSteps, random);
        var path = BuildMousePath(start, destination, steps, profile.UseCurvedPaths, random);

        foreach (var point in path)
        {
            await page.Mouse.MoveAsync((float)point.X, (float)point.Y, new MouseMoveOptions { Steps = 1 }).ConfigureAwait(false);
        }

        state.Position = destination;

        if (focus)
        {
            await locator.FocusAsync(new LocatorFocusOptions
            {
                Timeout = (float)Math.Max(500d, profile.PostActionDelay.MaxMs)
            }).ConfigureAwait(false);
        }

        return new LocatorTarget(destination, relative, box);
    }

    private async Task<ILocator> ResolveLocatorAsync(IPage page, HumanizedAction action, CancellationToken cancellationToken)
    {
        if (action.Target is null || action.Target.IsEmpty())
        {
            throw new InvalidOperationException($"鍔ㄤ綔 {action.Type} 闇€瑕佸畾浣嶄俊鎭€?");
        }

        return await _locatorBuilder.ResolveAsync(page, action.Target, cancellationToken).ConfigureAwait(false);
    }

    private HumanBehaviorProfileOptions ResolveProfile(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key) && _behaviorOptions.Profiles.TryGetValue(key, out var specific))
        {
            return specific;
        }

        if (_behaviorOptions.Profiles.TryGetValue(_behaviorOptions.DefaultProfile, out var fallback))
        {
            return fallback;
        }

        return HumanBehaviorProfileOptions.CreateDefault();
    }

    private Random GetRandom(string? key, HumanBehaviorProfileOptions profile)
    {
        var cacheKey = string.IsNullOrWhiteSpace(key) ? _behaviorOptions.DefaultProfile : key.Trim();

        if (!string.IsNullOrWhiteSpace(profile.RandomSeed))
        {
            cacheKey = $"seed:{cacheKey}:{profile.RandomSeed}";
            return _profileRandoms.GetOrAdd(cacheKey, _ => new Random(DeriveSeed(profile.RandomSeed!)));
        }

        return _profileRandoms.GetOrAdd(cacheKey, _ =>
        {
            lock (_randomLock)
            {
                return new Random(_baseRandom.Next());
            }
        });
    }

    private static int DeriveSeed(string seed)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
        return BitConverter.ToInt32(hash, 0);
    }

    private static TimeSpan SampleDelay(DelayRangeOptions range, Random random)
    {
        var (min, max) = range.Normalize();
        var millis = random.Next(min, max + 1);
        return TimeSpan.FromMilliseconds(millis);
    }

    private static int SampleInt(IntRangeOptions range, Random random)
    {
        var (min, max) = range.Normalize();
        return random.Next(min, max + 1);
    }

    private static Point PickPointInBox(LocatorBoundingBoxResult box, PixelRangeOptions jitterOptions, double edgeProbability, Random random)
    {
        var baseX = box.X + (box.Width * NextGaussian(random, 0.5, 0.18));
        var baseY = box.Y + (box.Height * NextGaussian(random, 0.5, 0.18));

        var (jitterMin, jitterMax) = jitterOptions.Normalize();
        if (jitterMax > 0)
        {
            var jitterX = random.Next(jitterMin, jitterMax + 1) * (random.NextDouble() < 0.5 ? -1 : 1);
            var jitterY = random.Next(jitterMin, jitterMax + 1) * (random.NextDouble() < 0.5 ? -1 : 1);
            baseX = Clamp(baseX + jitterX, box.X + 1, box.X + box.Width - 1);
            baseY = Clamp(baseY + jitterY, box.Y + 1, box.Y + box.Height - 1);
        }

        if (edgeProbability > 0 && random.NextDouble() < edgeProbability)
        {
            var horizontalEdge = random.NextDouble() < 0.5;
            var edgeOffsetX = horizontalEdge ? random.NextDouble() * box.Width : 0;
            var edgeOffsetY = horizontalEdge ? 0 : random.NextDouble() * box.Height;

            if (horizontalEdge)
            {
                baseX = box.X + edgeOffsetX;
                baseY = random.NextDouble() < 0.5 ? box.Y + 2 : box.Y + box.Height - 2;
            }
            else
            {
                baseX = random.NextDouble() < 0.5 ? box.X + 2 : box.X + box.Width - 2;
                baseY = box.Y + edgeOffsetY;
            }

            baseX = Clamp(baseX, box.X + 1, box.X + box.Width - 1);
            baseY = Clamp(baseY, box.Y + 1, box.Y + box.Height - 1);
        }

        return new Point(baseX, baseY);
    }

    private static double NextGaussian(Random random, double mean, double stddev)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        var value = mean + stddev * randStdNormal;
        return Math.Min(1.0, Math.Max(0.0, value));
    }

    private static Point PickRandomEntryPoint(Point destination, Random random)
    {
        var radius = 80 + (random.NextDouble() * 120);
        var angle = random.NextDouble() * Math.PI * 2;
        var x = destination.X + Math.Cos(angle) * radius;
        var y = destination.Y + Math.Sin(angle) * radius;
        return new Point(x, y);
    }

    private static List<Point> BuildMousePath(Point start, Point end, int steps, bool curved, Random random)
    {
        steps = Math.Max(steps, 3);
        var points = new List<Point>(steps);

        if (curved)
        {
            var control = new Point(
                (start.X + end.X) / 2 + random.NextDouble() * 40 - 20,
                (start.Y + end.Y) / 2 + random.NextDouble() * 60 - 30);

            for (var i = 1; i <= steps; i++)
            {
                var t = i / (double)steps;
                var x = Math.Pow(1 - t, 2) * start.X + 2 * (1 - t) * t * control.X + Math.Pow(t, 2) * end.X;
                var y = Math.Pow(1 - t, 2) * start.Y + 2 * (1 - t) * t * control.Y + Math.Pow(t, 2) * end.Y;
                points.Add(new Point(x, y));
            }
        }
        else
        {
            for (var i = 1; i <= steps; i++)
            {
                var t = i / (double)steps;
                var x = start.X + ((end.X - start.X) * t);
                var y = start.Y + ((end.Y - start.Y) * t);
                points.Add(new Point(x, y));
            }
        }

        return points;
    }

    private async Task<LocatorBoundingBoxResult> EnsureBoundingBoxAsync(ILocator locator)
    {
        var box = await locator.BoundingBoxAsync().ConfigureAwait(false);
        if (box is null)
        {
            throw new InvalidOperationException("鐩爣鍏冪礌涓嶅彲瑙佹垨鏈覆鏌撱€?");
        }

        return box;
    }

    private static char GenerateTypo(Random random, char reference)
    {
        const string candidates = "abcdefghijklmnopqrstuvwxyz0123456789";
        var ch = char.ToLowerInvariant(reference);
        var index = random.Next(candidates.Length);
        var typo = candidates[index];
        if (typo == ch)
        {
            typo = candidates[(index + 1) % candidates.Length];
        }

        return char.IsUpper(reference) ? char.ToUpperInvariant(typo) : typo;
    }

    private static Task MaybeApplyIdlePauseAsync(HumanizedAction action, CancellationToken cancellationToken)
        => action.Timing.IdlePause > TimeSpan.Zero
            ? Task.Delay(action.Timing.IdlePause, cancellationToken)
            : Task.CompletedTask;

    private async Task MaybeRandomIdleAsync(HumanBehaviorProfileOptions profile, Random random, CancellationToken cancellationToken)
    {
        if (profile.RandomIdleProbability <= 0 || random.NextDouble() >= profile.RandomIdleProbability)
        {
            return;
        }

        var pause = SampleDelay(profile.RandomIdleDuration, random);
        if (pause > TimeSpan.Zero)
        {
            await Task.Delay(pause, cancellationToken).ConfigureAwait(false);
        }
    }

    private ClickStrategy ResolveClickStrategy(HumanizedAction action, HumanBehaviorProfileOptions profile)
    {
        var jitter = new PixelRangeOptions(profile.ClickJitter.MinPx, profile.ClickJitter.MaxPx);
        var edgeProbability = profile.EdgeClickProbability;

        if (action.Target is { Role: { } role })
        {
            switch (role)
            {
                case AriaRole.Textbox:
                    jitter = new PixelRangeOptions(Math.Max(0, jitter.MinPx - 1), Math.Max(1, Math.Min(jitter.MaxPx, 3)));
                    edgeProbability = Math.Min(edgeProbability * 0.3, 0.08);
                    break;
                case AriaRole.Button:
                    edgeProbability = Math.Clamp(edgeProbability + 0.05, 0.05, 0.35);
                    break;
                case AriaRole.Link:
                case AriaRole.Img:
                    edgeProbability = Math.Clamp(edgeProbability + 0.15, 0.1, 0.45);
                    break;
            }
        }

        return new ClickStrategy(jitter, Math.Clamp(edgeProbability, 0, 0.5));
    }

    private MouseState GetMouseState(IPage page)
        => _mouseStates.GetValue(page, _ => new MouseState());

    private static double Clamp(double value, double min, double max)
        => Math.Min(Math.Max(value, min), max);

    private sealed record LocatorTarget(Point Absolute, Point Relative, LocatorBoundingBoxResult Box);
    private readonly record struct ClickStrategy(PixelRangeOptions Jitter, double EdgeProbability);

    private sealed class MouseState
    {
        public Point? Position { get; set; }
    }

    private sealed record Point(double X, double Y);
}


