// SPDX-FileCopyrightText: 2023 JJ <47927305+PHCodes@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Objectives.Systems;

namespace Content.Server.Objectives.Components;

/// <summary>
/// Requires that the player dies to be complete.
/// </summary>
[RegisterComponent, Access(typeof(BecomeGolemConditionSystem))]
public sealed partial class BecomeGolemConditionComponent : Component
{
}