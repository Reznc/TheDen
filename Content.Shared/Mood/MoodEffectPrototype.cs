// SPDX-FileCopyrightText: 2024 DEATHB4DEFEAT <77995199+DEATHB4DEFEAT@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 VMSolidus <evilexecutive@gmail.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Robust.Shared.Prototypes;

namespace Content.Shared.Mood;

[Prototype]
public sealed partial class MoodEffectPrototype : IPrototype
{
    /// <summary>
    ///     The ID of the moodlet to use.
    /// </summary>
    [IdDataField]
    public string ID { get; } = default!;

    public string Description => Loc.GetString($"mood-effect-{ID}");

    /// <summary>
    ///     If they already have an effect with the same category, the new one will replace the old one.
    /// </summary>
    [DataField, ValidatePrototypeId<MoodCategoryPrototype>]
    public string? Category;

    /// <summary>
    ///     How much should this moodlet modify an entity's Mood.
    /// </summary>
    [DataField(required: true)]
    public float MoodChange;

    /// <summary>
    ///     How long, in Seconds, does this moodlet last? If omitted, the moodlet will last until canceled by any system.
    /// </summary>
    [DataField]
    public int Timeout;

    /// <summary>
    ///     Should this moodlet be hidden from the player? EG: No popups or chat messages.
    /// </summary>
    [DataField]
    public bool Hidden;

    /// <summary>
    ///     When not null, this moodlet will replace itself with another Moodlet upon expiration.
    /// </summary>
    [DataField]
    public ProtoId<MoodEffectPrototype>? MoodletOnEnd;
}
