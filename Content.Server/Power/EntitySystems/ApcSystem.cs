// SPDX-FileCopyrightText: 2022 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 Moony <moonheart08@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Rane <60792108+Elijahrane@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 rolfero <45628623+rolfero@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <drsmugleaf@gmail.com>
// SPDX-FileCopyrightText: 2023 James Simonson <jamessimo89@gmail.com>
// SPDX-FileCopyrightText: 2023 Kevin Zheng <kevinz5000@gmail.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Pieter-Jan Briers <pieterjan.briers@gmail.com>
// SPDX-FileCopyrightText: 2023 Slava0135 <40753025+Slava0135@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2023 keronshb <54602815+keronshb@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 DEATHB4DEFEAT <77995199+DEATHB4DEFEAT@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 WarMechanic <69510347+WarMechanic@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Server.Emp;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.Pow3r;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.APC;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Power.EntitySystems;

public sealed class ApcSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(PowerNetSystem));

        SubscribeLocalEvent<ApcComponent, BoundUIOpenedEvent>(OnBoundUiOpen);
        SubscribeLocalEvent<ApcComponent, MapInitEvent>(OnApcInit);
        SubscribeLocalEvent<ApcComponent, ChargeChangedEvent>(OnBatteryChargeChanged);
        SubscribeLocalEvent<ApcComponent, ApcToggleMainBreakerMessage>(OnToggleMainBreaker);
        SubscribeLocalEvent<ApcComponent, GotEmaggedEvent>(OnEmagged);

        SubscribeLocalEvent<ApcComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<ApcComponent, EmpDisabledRemoved>(OnEmpDisabledRemoved);
    }

    public override void Update(float deltaTime)
    {
        var query = EntityQueryEnumerator<ApcComponent, PowerNetworkBatteryComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out var apc, out var battery, out var ui))
        {
            if (apc.LastUiUpdate + ApcComponent.VisualsChangeDelay < _gameTiming.CurTime)
            {
                apc.LastUiUpdate = _gameTiming.CurTime;
                UpdateUIState(uid, apc, battery);
            }
        }
    }

    // Change the APC's state only when the battery state changes, or when it's first created.
    private void OnBatteryChargeChanged(EntityUid uid, ApcComponent component, ref ChargeChangedEvent args)
    {
        UpdateApcState(uid, component);
    }

    private void OnApcInit(EntityUid uid, ApcComponent component, MapInitEvent args)
    {
        UpdateApcState(uid, component);
    }

    //Update the HasAccess var for UI to read
    private void OnBoundUiOpen(EntityUid uid, ApcComponent component, BoundUIOpenedEvent args)
    {
        // TODO: this should be per-player not stored on the apc
        component.HasAccess = _accessReader.IsAllowed(args.Actor, uid);
        UpdateApcState(uid, component);
    }

    private void OnToggleMainBreaker(EntityUid uid, ApcComponent component, ApcToggleMainBreakerMessage args)
    {
        var attemptEv = new ApcToggleMainBreakerAttemptEvent();
        RaiseLocalEvent(uid, ref attemptEv);
        if (attemptEv.Cancelled)
        {
            _popup.PopupCursor(Loc.GetString("apc-component-on-toggle-cancel"),
                args.Actor, PopupType.Medium);
            return;
        }

        if (_accessReader.IsAllowed(args.Actor, uid))
        {
            ApcToggleBreaker(uid, component);
        }
        else
        {
            _popup.PopupCursor(Loc.GetString("apc-component-insufficient-access"),
                args.Actor, PopupType.Medium);
        }
    }

    public void ApcToggleBreaker(EntityUid uid, ApcComponent? apc = null, PowerNetworkBatteryComponent? battery = null)
    {
        if (!Resolve(uid, ref apc, ref battery))
            return;

        apc.MainBreakerEnabled = !apc.MainBreakerEnabled;
        battery.CanDischarge = apc.MainBreakerEnabled;

        UpdateUIState(uid, apc);
        _audio.PlayPvs(apc.OnReceiveMessageSound, uid, AudioParams.Default.WithVolume(-2f));
    }

    private void OnEmagged(EntityUid uid, ApcComponent comp, ref GotEmaggedEvent args)
    {
        // no fancy conditions
        args.Handled = true;
    }

    public void UpdateApcState(EntityUid uid,
        ApcComponent? apc=null,
        PowerNetworkBatteryComponent? battery = null)
    {
        if (!Resolve(uid, ref apc, ref battery, false))
            return;

        var newState = CalcChargeState(uid, battery.NetworkBattery);
        if (newState != apc.LastChargeState && apc.LastChargeStateTime + ApcComponent.VisualsChangeDelay < _gameTiming.CurTime)
        {
            apc.LastChargeState = newState;
            apc.LastChargeStateTime = _gameTiming.CurTime;

            if (TryComp(uid, out AppearanceComponent? appearance))
            {
                _appearance.SetData(uid, ApcVisuals.ChargeState, newState, appearance);
            }
        }

        var extPowerState = CalcExtPowerState(uid, battery.NetworkBattery);
        if (extPowerState != apc.LastExternalState)
        {
            apc.LastExternalState = extPowerState;
            UpdateUIState(uid, apc, battery);
        }
    }

    public void UpdateUIState(EntityUid uid,
        ApcComponent? apc = null,
        PowerNetworkBatteryComponent? netBat = null,
        UserInterfaceComponent? ui = null)
    {
        if (!Resolve(uid, ref apc, ref netBat, ref ui))
            return;

        var battery = netBat.NetworkBattery;

        var state = new ApcBoundInterfaceState(apc.MainBreakerEnabled, apc.HasAccess,
            (int) MathF.Ceiling(battery.CurrentSupply), apc.LastExternalState,
            battery.CurrentStorage / battery.Capacity);

        _ui.SetUiState((uid, ui), ApcUiKey.Key, state);
    }

    private ApcChargeState CalcChargeState(EntityUid uid, PowerState.Battery battery)
    {
        if (HasComp<EmaggedComponent>(uid) || HasComp<EmpDisabledComponent>(uid))
            return ApcChargeState.Emag;

        if (battery.CurrentStorage / battery.Capacity > ApcComponent.HighPowerThreshold)
        {
            return ApcChargeState.Full;
        }

        var delta = battery.CurrentSupply - battery.CurrentReceiving;
        return delta < 0 ? ApcChargeState.Charging : ApcChargeState.Lack;
    }

    private ApcExternalPowerState CalcExtPowerState(EntityUid uid, PowerState.Battery battery)
    {
        if (battery.CurrentReceiving == 0 && !MathHelper.CloseTo(battery.CurrentStorage / battery.Capacity, 1))
        {
            return ApcExternalPowerState.None;
        }

        var delta = battery.CurrentSupply - battery.CurrentReceiving;
        if (!MathHelper.CloseToPercent(delta, 0, 0.1f) && delta < 0)
        {
            return ApcExternalPowerState.Low;
        }

        return ApcExternalPowerState.Good;
    }
    
    private void OnEmpPulse(EntityUid uid, ApcComponent component, ref EmpPulseEvent args)
    {
        EnsureComp<EmpDisabledComponent>(uid, out var emp); //event calls before EmpDisabledComponent is added, ensure it to force sprite update
        UpdateApcState(uid);
    }

    private void OnEmpDisabledRemoved(EntityUid uid, ApcComponent component, ref EmpDisabledRemoved args)
    {
        UpdateApcState(uid);
    }
}

[ByRefEvent]
public record struct ApcToggleMainBreakerAttemptEvent(bool Cancelled);
