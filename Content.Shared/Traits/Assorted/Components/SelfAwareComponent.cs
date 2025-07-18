// SPDX-FileCopyrightText: 2024 Angelo Fallaria <ba.fallaria@gmail.com>
// SPDX-FileCopyrightText: 2024 DEATHB4DEFEAT <77995199+DEATHB4DEFEAT@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Skubman <ba.fallaria@gmail.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

namespace Content.Shared.Traits.Assorted.Components;

/// <summary>
///     This is used for the Self-Aware trait to enhance the information received from HealthExaminableSystem.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SelfAwareComponent : Component
{
    // <summary>
    //     Damage types that an entity is able to precisely analyze like a health analyzer when they examine themselves.
    // </summary>
    [DataField(required: true, customTypeSerializer:typeof(PrototypeIdHashSetSerializer<DamageTypePrototype>)), AutoNetworkedField]
    public HashSet<string> AnalyzableTypes = default!;

    // <summary>
    //     Damage groups that an entity is able to detect the presence of when they examine themselves.
    // </summary>
    [DataField(required: true, customTypeSerializer:typeof(PrototypeIdHashSetSerializer<DamageGroupPrototype>)), AutoNetworkedField]
    public HashSet<string> DetectableGroups = default!;

    // <summary>
    //     The thresholds for determining the examine text of DetectableGroups for certain amounts of damage.
    //     These are calculated as a percentage of the entity's critical threshold.
    // </summary>
    public List<FixedPoint2> Thresholds = new()
        { FixedPoint2.New(0.10), FixedPoint2.New(0.25), FixedPoint2.New(0.40), FixedPoint2.New(0.60) };
}
