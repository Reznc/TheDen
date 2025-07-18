// SPDX-FileCopyrightText: 2023 Debug <49997488+DebugOk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2024 Raphael Bertoche <bertocheraphael@gmail.com>
// SPDX-FileCopyrightText: 2024 VMSolidus <evilexecutive@gmail.com>
// SPDX-FileCopyrightText: 2025 Eris <eris@erisws.com>
// SPDX-FileCopyrightText: 2025 Spatison <137375981+Spatison@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Timfa <timfalken@hotmail.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Materials;

/// <summary>
/// This handles storing materials and modifying their amounts
/// <see cref="MaterialStorageComponent"/>
/// </summary>
public abstract class SharedMaterialStorageSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly SharedMaterialSiloSystem _materialSilo = default!;

    /// <summary>
    /// Default volume for a sheet if the material's entity prototype has no material composition.
    /// </summary>
    private const int DefaultSheetVolume = 100;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MaterialStorageComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MaterialStorageComponent, InteractUsingEvent>(OnInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<InsertingMaterialStorageComponent>();
        while (query.MoveNext(out var uid, out var inserting))
        {
            if (_timing.CurTime < inserting.EndTime)
                continue;

            _appearance.SetData(uid, MaterialStorageVisuals.Inserting, false);
            RemComp(uid, inserting);
        }
    }

    private void OnMapInit(EntityUid uid, MaterialStorageComponent component, MapInitEvent args)
    {
        _appearance.SetData(uid, MaterialStorageVisuals.Inserting, false);
    }

    /// <summary>
    /// Gets the volume of a specified material contained in this storage.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="material"></param>
    /// <param name="component"></param>
    /// <returns>The volume of the material</returns>
    [PublicAPI]
    public int GetMaterialAmount(EntityUid uid, MaterialPrototype material, MaterialStorageComponent? component = null)
    {
        return GetMaterialAmount(uid, material.ID, component);
    }

    /// <summary>
    /// Gets the volume of a specified material contained in this storage.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="material"></param>
    /// <param name="component"></param>
    /// <param name="utilizer"></param>
    /// <returns>The volume of the material</returns>
    public int GetMaterialAmount(EntityUid uid, string material, MaterialStorageComponent? component = null, MaterialSiloUtilizerComponent? utilizer = null)
    {
        if (!Resolve(uid, ref component))
            return 0; //you have nothing

        if (Resolve(uid, ref utilizer, false) && utilizer.Silo.HasValue)
            return _materialSilo.GetSiloMaterialAmount(uid, material, utilizer);

        return component.Storage.GetValueOrDefault(material, 0);
    }

    /// <summary>
    /// Gets the total volume of all materials in the storage.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="utilizer"></param>
    /// <returns>The volume of all materials in the storage</returns>
    public int GetTotalMaterialAmount(EntityUid uid, MaterialStorageComponent? component = null, MaterialSiloUtilizerComponent? utilizer = null)
    {
        if (!Resolve(uid, ref component))
            return 0;

        if (Resolve(uid, ref utilizer, false) && utilizer.Silo.HasValue)
            return _materialSilo.GetSiloTotalMaterialAmount(uid, utilizer);

        return component.Storage.Values.Sum();
    }

    /// <summary>
    /// Tests if a specific amount of volume will fit in the storage.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="volume"></param>
    /// <param name="component"></param>
    /// <param name="utilizer"></param>
    /// <returns>If the specified volume will fit</returns>
    public bool CanTakeVolume(EntityUid uid, int volume, MaterialStorageComponent? component = null, MaterialSiloUtilizerComponent? utilizer = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var storageLimit = component.StorageLimit;
        if (Resolve(uid, ref utilizer, false) && utilizer.Silo.HasValue)
            storageLimit = _materialSilo.GetSiloStorage(uid, utilizer)?.Comp.StorageLimit;

        return storageLimit == null || GetTotalMaterialAmount(uid, component) + volume <= storageLimit;
    }

    /// <summary>
    /// Checks if the specified material can be changed by the specified volume.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="materialId"></param>
    /// <param name="volume"></param>
    /// <param name="component"></param>
    /// <param name="utilizer"></param>
    /// <returns>If the amount can be changed</returns>
    public bool CanChangeMaterialAmount(EntityUid uid, string materialId, int volume, MaterialStorageComponent? component = null, MaterialSiloUtilizerComponent? utilizer = null)
    {
        if (!Resolve(uid, ref component)
            || !CanTakeVolume(uid, volume, component, utilizer)
            || (!component.MaterialWhiteList?.Contains(materialId) ?? false))
            return false;

        var amount = GetMaterialAmount(uid, materialId, component, utilizer);
        return amount + volume >= 0;
    }

    /// <summary>
    /// Checks if the specified materials can be changed by the specified volumes.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="materials"></param>
    /// <param name="utilizer"></param>
    /// <returns>If the amount can be changed</returns>
    public bool CanChangeMaterialAmount(Entity<MaterialStorageComponent?> entity, Dictionary<string,int> materials, MaterialSiloUtilizerComponent? utilizer = null)
    {
        if (!Resolve(entity, ref entity.Comp))
            return false;

        foreach (var (material, amount) in materials)
        {
            if (!CanChangeMaterialAmount(entity, material, amount, entity.Comp, utilizer))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Changes the amount of a specific material in the storage.
    /// Still respects the filters in place.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="materialId"></param>
    /// <param name="volume"></param>
    /// <param name="component"></param>
    /// <param name="utilizer"></param>
    /// <param name="dirty"></param>
    /// <returns>If it was successful</returns>
    public bool TryChangeMaterialAmount(EntityUid uid, string materialId, int volume, MaterialStorageComponent? component = null, MaterialSiloUtilizerComponent? utilizer = null, bool dirty = true)
    {
        if (!Resolve(uid, ref component))
            return false;

        var storage = component;
        var storageUid = uid;
        if (Resolve(uid, ref utilizer, false) && utilizer.Silo.HasValue)
        {
            var silo = _materialSilo.GetSiloStorage(uid, utilizer);
            if (silo.HasValue)
            {
                storage = silo.Value.Comp;
                storageUid = silo.Value;
            }
        }

        if (!CanChangeMaterialAmount(uid, materialId, volume, component, utilizer))
            return false;

        storage.Storage.TryAdd(materialId, 0);
        storage.Storage[materialId] += volume;

        var ev = new MaterialAmountChangedEvent();
        RaiseLocalEvent(storageUid, ref ev);

        if (dirty)
            Dirty(storageUid, storage);
        return true;
    }

    /// <summary>
    /// Changes the amount of a specific material in the storage.
    /// Still respects the filters in place.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="materials"></param>
    /// <returns>If the amount can be changed</returns>
    public bool TryChangeMaterialAmount(Entity<MaterialStorageComponent?> entity, Dictionary<string,int> materials)
    {
        if (!Resolve(entity, ref entity.Comp))
            return false;

        var storage = entity.Comp;
        var storageUid = entity;
        if (TryComp<MaterialSiloUtilizerComponent>(entity, out var utilizer) && utilizer.Silo.HasValue)
        {
            var silo = _materialSilo.GetSiloStorage(entity, utilizer);
            if (silo.HasValue)
            {
                storage = silo.Value.Comp;
                storageUid = silo.Value.Owner;
            }
        }

        if (!CanChangeMaterialAmount(entity, materials, utilizer))
            return false;

        foreach (var (material, amount) in materials)
        {
            if (!TryChangeMaterialAmount(entity, material, amount, entity.Comp, utilizer, false))
                return false;
        }

        Dirty(storageUid, storage);
        return true;
    }

    /// <summary>
    /// Tries to set the amount of material in the storage to a specific value.
    /// Still respects the filters in place.
    /// </summary>
    /// <param name="uid">The entity to change the material storage on.</param>
    /// <param name="materialId">The ID of the material to change.</param>
    /// <param name="volume">The stored material volume to set the storage to.</param>
    /// <param name="component">The storage component on <paramref name="uid"/>. Resolved automatically if not given.</param>
    /// <param name="utilizer">The material silo utilizer component on <paramref name="uid"/>.</param>
    /// <returns>True if it was successful (enough space etc).</returns>
    public bool TrySetMaterialAmount(
        EntityUid uid,
        string materialId,
        int volume,
        MaterialStorageComponent? component = null,
        MaterialSiloUtilizerComponent? utilizer = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var curAmount = GetMaterialAmount(uid, materialId, component, utilizer);
        var delta = volume - curAmount;
        return TryChangeMaterialAmount(uid, materialId, delta, component, utilizer);
    }

    public virtual bool CanInsertMaterialEntity(
        EntityUid toInsert,
        EntityUid receiver,
        MaterialStorageComponent? storage = null,
        MaterialSiloUtilizerComponent? utilizer = null,
        MaterialComponent? material = null,
        PhysicalCompositionComponent? composition = null
    )
    {
        if (!Resolve(receiver, ref storage)
            || !Resolve(toInsert, ref material, ref composition, false)
            || _whitelistSystem.IsWhitelistFail(storage.Whitelist, toInsert)
            || HasComp<UnremoveableComponent>(toInsert))
            return false;

        // Material Whitelist checked implicitly by CanChangeMaterialAmount();

        var multiplier = TryComp<StackComponent>(toInsert, out var stackComponent) ? stackComponent.Count : 1;
        var totalVolume = 0;
        foreach (var (mat, vol) in composition.MaterialComposition)
        {
            if (!CanChangeMaterialAmount(receiver, mat, vol * multiplier, storage, utilizer))
                return false;
            totalVolume += vol * multiplier;
        }

        return CanTakeVolume(receiver, totalVolume, storage, utilizer);
    }

    /// <summary>
    /// Tries to insert an entity into the material storage.
    /// </summary>
    public virtual bool TryInsertMaterialEntity(EntityUid user,
        EntityUid toInsert,
        EntityUid receiver,
        MaterialStorageComponent? storage = null,
        MaterialSiloUtilizerComponent? utilizer = null,
        MaterialComponent? material = null,
        PhysicalCompositionComponent? composition = null)
    {
        if (!Resolve(receiver, ref storage))
            return false;

        if (!Resolve(toInsert, ref material, ref composition, false))
            return false;

        if (_whitelistSystem.IsWhitelistFail(storage.Whitelist, toInsert))
            return false;

        if (HasComp<UnremoveableComponent>(toInsert))
            return false;

        // Material Whitelist checked implicitly by CanChangeMaterialAmount();

        var multiplier = TryComp<StackComponent>(toInsert, out var stackComponent) ? stackComponent.Count : 1;
        var totalVolume = 0;
        foreach (var (mat, vol) in composition.MaterialComposition)
        {
            if (!CanChangeMaterialAmount(receiver, mat, vol * multiplier, storage, utilizer))
                return false;
            totalVolume += vol * multiplier;
        }

        if (!CanTakeVolume(receiver, totalVolume, storage, utilizer))
            return false;

        foreach (var (mat, vol) in composition.MaterialComposition)
        {
            TryChangeMaterialAmount(receiver, mat, vol * multiplier, storage, utilizer);
        }

        var insertingComp = EnsureComp<InsertingMaterialStorageComponent>(receiver);
        insertingComp.EndTime = _timing.CurTime + storage.InsertionTime;
        if (!storage.IgnoreColor)
        {
            _prototype.TryIndex<MaterialPrototype>(composition.MaterialComposition.Keys.First(), out var lastMat);
            insertingComp.MaterialColor = lastMat?.Color;
        }
        _appearance.SetData(receiver, MaterialStorageVisuals.Inserting, true);
        Dirty(receiver, insertingComp);

        var ev = new MaterialEntityInsertedEvent(user, toInsert, material, multiplier); // Lavaland Change
        RaiseLocalEvent(receiver, ref ev);
        return true;
    }

    /// <summary>
    /// Broadcasts an event that will collect a list of which materials
    /// are allowed to be inserted into the materialStorage.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    public void UpdateMaterialWhitelist(EntityUid uid, MaterialStorageComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;
        var ev = new GetMaterialWhitelistEvent(uid);
        RaiseLocalEvent(uid, ref ev);
        component.MaterialWhiteList = ev.Whitelist;
        Dirty(uid, component);
    }

    private void OnInteractUsing(EntityUid uid, MaterialStorageComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !component.InsertOnInteract)
            return;
        args.Handled = TryInsertMaterialEntity(args.User, args.Used, uid, component);
    }

    public int GetSheetVolume(MaterialPrototype material)
    {
        if (material.StackEntity == null)
            return DefaultSheetVolume;

        var proto = _prototype.Index<EntityPrototype>(material.StackEntity);

        if (!proto.TryGetComponent<PhysicalCompositionComponent>(out var composition))
            return DefaultSheetVolume;

        return composition.MaterialComposition.FirstOrDefault(kvp => kvp.Key == material.ID).Value;
    }
}
