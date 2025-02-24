﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class BlockEntityGroundStorage : BlockEntityDisplay, IBlockEntityContainer, ITexPositionSource
    {
        public object inventoryLock = new object(); // Because OnTesselation runs in another thread

        protected InventoryGeneric inventory;

        public GroundStorageProperties StorageProps { get; protected set; }
        public bool forceStorageProps = false;
        protected EnumGroundStorageLayout? overrideLayout;

        public int TransferQuantity => StorageProps.TransferQuantity;
        public int BulkTransferQuantity => StorageProps.Layout == EnumGroundStorageLayout.Stacking ? StorageProps.BulkTransferQuantity : 1;

        protected virtual int invSlotCount => 4;
        protected Cuboidf[] colSelBoxes;

        ItemSlot isUsingSlot;

        public int TotalStackSize
        {
            get
            {
                int sum = 0;
                foreach (var slot in inventory) sum += slot.StackSize;
                return sum;
            }
        }

        public int Capacity
        {
            get { 
                switch (StorageProps.Layout)
                {
                    case EnumGroundStorageLayout.SingleCenter: return 1;
                    case EnumGroundStorageLayout.Halves: return 2;
                    case EnumGroundStorageLayout.Quadrants: return 4;
                    case EnumGroundStorageLayout.Stacking: return StorageProps.StackingCapacity;
                    default: return 1;
                }
            }
        }
        
        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        public override string InventoryClassName
        {
            get { return "groundstorage"; }
        }

        public override string AttributeTransformCode => "groundStorageTransform";

        public override TextureAtlasPosition this[string textureCode]
        {
            get
            {
                // Prio 1: Get from list of explicility defined textures
                if (StorageProps.Layout == EnumGroundStorageLayout.Stacking && StorageProps.StackingTextures != null)
                {
                    if (StorageProps.StackingTextures.TryGetValue(textureCode, out var texturePath))
                    {
                        return getOrCreateTexPos(texturePath);
                    }
                }

                // Prio 2: Try other texture sources
                return base[textureCode];
            }
        }


        public BlockEntityGroundStorage() : base()
        {
            meshes = new MeshData[invSlotCount];
            inventory = new InventoryGeneric(invSlotCount, null, null, (int slotId, InventoryGeneric inv) => new ItemSlot(inv));
            foreach (var slot in inventory)
            {
                slot.StorageType |= EnumItemStorageFlags.Backpack;
            }

            colSelBoxes = new Cuboidf[] { new Cuboidf(0, 0, 0, 1, 0.25f, 1) };
        }


        public void ForceStorageProps(GroundStorageProperties storageProps)
        {
            StorageProps = storageProps;
            forceStorageProps = true;
        }


        public override void Initialize(ICoreAPI api)
        {
            capi = api as ICoreClientAPI;
            base.Initialize(api);

            DetermineStorageProperties(null);

            if (capi != null)
            {
                updateMeshes();
            }
        }


        public Cuboidf[] GetSelectionBoxes()
        {
            return colSelBoxes;
        }

        public Cuboidf[] GetCollisionBoxes()
        {
            return colSelBoxes;
        }

        public virtual bool OnPlayerInteractStart(IPlayer player, BlockSelection bs)
        {
            ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && !hotbarSlot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorGroundStorable>()) return false;

            if (!BlockBehaviorReinforcable.AllowRightClickPickup(Api.World, Pos, player)) return false;

            DetermineStorageProperties(hotbarSlot);

            bool ok = false;

            if (StorageProps != null)
            {
                if (StorageProps.Layout == EnumGroundStorageLayout.Quadrants && inventory.Empty)
                {
                    double dx = Math.Abs(bs.HitPosition.X - 0.5);
                    double dz = Math.Abs(bs.HitPosition.X - 0.5);
                    if (dx < 2 / 16f && dz < 2 / 16f)
                    {
                        overrideLayout = EnumGroundStorageLayout.SingleCenter;
                        DetermineStorageProperties(hotbarSlot);
                    }
                }

                switch (StorageProps.Layout)
                {
                    case EnumGroundStorageLayout.SingleCenter:
                        ok = putOrGetItemSingle(inventory[0], player, bs);
                        break;


                    case EnumGroundStorageLayout.Halves:
                        if (bs.HitPosition.X < 0.5)
                        {
                            ok = putOrGetItemSingle(inventory[0], player, bs);
                        }
                        else
                        {
                            ok = putOrGetItemSingle(inventory[1], player, bs);
                        }
                        break;

                    case EnumGroundStorageLayout.Quadrants:
                        int pos = ((bs.HitPosition.X > 0.5) ? 2 : 0) + ((bs.HitPosition.Z > 0.5) ? 1 : 0);
                        ok = putOrGetItemSingle(inventory[pos], player, bs);
                        break;

                    case EnumGroundStorageLayout.Stacking:
                        ok = putOrGetItemStacking(player, bs);
                        break;
                }
            }

            if (ok)
            {
                MarkDirty(true);
                updateMeshes();
            }

            if (inventory.Empty) Api.World.BlockAccessor.SetBlock(0, Pos);

            return ok;
        }



        public bool OnPlayerInteractStep(float secondsUsed, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (isUsingSlot?.Itemstack?.Collectible is IContainedInteractable collIci)
            {
                return collIci.OnContainedInteractStep(secondsUsed, this, isUsingSlot, byPlayer, blockSel);
            }

            isUsingSlot = null;
            return false;
        }


        public void OnPlayerInteractStop(float secondsUsed, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (isUsingSlot?.Itemstack.Collectible is IContainedInteractable collIci)
            {
                collIci.OnContainedInteractStop(secondsUsed, this, isUsingSlot, byPlayer, blockSel);
            }

            isUsingSlot = null;
        }






        public ItemSlot GetSlotAt(BlockSelection bs)
        {
            if (StorageProps == null) return null;

            switch (StorageProps.Layout)
            {
                case EnumGroundStorageLayout.SingleCenter:
                    return inventory[0];

                case EnumGroundStorageLayout.Halves:
                    if (bs.HitPosition.X < 0.5)
                    {
                        return inventory[0];
                    }
                    else
                    {
                        return inventory[1];
                    }

                case EnumGroundStorageLayout.Quadrants:
                    int pos = ((bs.HitPosition.X > 0.5) ? 2 : 0) + ((bs.HitPosition.Z > 0.5) ? 1 : 0);
                    return inventory[pos];

                case EnumGroundStorageLayout.Stacking:
                    return inventory[0];
            }

            return null;
        }



        public bool OnTryCreateKiln()
        {
            ItemStack stack = inventory.FirstNonEmptySlot.Itemstack;
            if (stack == null) return false;

            if (stack.StackSize > StorageProps.MaxFireable)
            {
                capi?.TriggerIngameError(this, "overfull", Lang.Get("Can only fire up to {0} at once.", StorageProps.MaxFireable));
                return false;
            }
            
            if (stack.Collectible.CombustibleProps == null || stack.Collectible.CombustibleProps.SmeltingType != EnumSmeltType.Fire)
            {
                capi?.TriggerIngameError(this, "notfireable", Lang.Get("This is not a fireable block or item", StorageProps.MaxFireable));
                return false;
            }


            return true;
        }

        public virtual void DetermineStorageProperties(ItemSlot sourceSlot)
        {
            ItemStack sourceStack = inventory.FirstNonEmptySlot?.Itemstack ?? sourceSlot?.Itemstack;

            if (!forceStorageProps)
            {
                if (StorageProps == null)
                {
                    if (sourceStack == null) return;

                    StorageProps = sourceStack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps;
                }
            }

            if (StorageProps == null) return;  // Seems necessary to avoid crash with certain items placed in game version 1.15-pre.1?

            if (StorageProps.CollisionBox != null)
            {
                colSelBoxes[0] = StorageProps.CollisionBox.Clone();
            } else
            {
                if (sourceStack?.Block != null)
                {
                    colSelBoxes[0] = sourceStack.Block.CollisionBoxes[0].Clone();
                }
                
            }
            if (StorageProps.CbScaleYByLayer != 0)
            {
                colSelBoxes[0] = colSelBoxes[0].Clone();
                colSelBoxes[0].Y2 *= ((int)Math.Ceiling(StorageProps.CbScaleYByLayer * inventory[0].StackSize) * 8) / 8;
            }

            if (overrideLayout != null)
            {
                StorageProps = StorageProps.Clone();
                StorageProps.Layout = (EnumGroundStorageLayout)overrideLayout;
            }
        }



        protected bool putOrGetItemStacking(IPlayer byPlayer, BlockSelection bs)
        {
            BlockPos abovePos = Pos.UpCopy();
            BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(abovePos);
            if (be is BlockEntityGroundStorage beg)
            {
                return beg.OnPlayerInteractStart(byPlayer, bs);
            }

            bool sneaking = byPlayer.Entity.Controls.Sneak;


            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (sneaking && TotalStackSize >= Capacity)
            {
                Block pileblock = Api.World.BlockAccessor.GetBlock(Pos);
                Block aboveblock = Api.World.BlockAccessor.GetBlock(abovePos);

                if (aboveblock.IsReplacableBy(pileblock))
                {
                    BlockGroundStorage bgs = pileblock as BlockGroundStorage;
                    var bsc = bs.Clone();
                    bsc.Position.Up();
                    bsc.Face = null;
                    return bgs.CreateStorage(Api.World, bsc, byPlayer);
                }

                return false;
            }


            bool equalStack = inventory[0].Empty || hotbarSlot.Itemstack != null && hotbarSlot.Itemstack.Equals(Api.World, inventory[0].Itemstack, GlobalConstants.IgnoredStackAttributes);

            if (sneaking && !equalStack)
            {
                return false;
            }

            lock (inventoryLock)
            {
                if (sneaking)
                {
                    return TryPutItem(byPlayer);
                }
                else
                {
                    return TryTakeItem(byPlayer);
                }
            }
        }


        public virtual bool TryPutItem(IPlayer player)
        {
            if (TotalStackSize >= Capacity) return false;

            ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;

            if (hotbarSlot.Itemstack == null) return false;

            ItemSlot invSlot = inventory[0];

            if (invSlot.Empty)
            {
                if (hotbarSlot.TryPutInto(Api.World, invSlot, 1) > 0)
                {
                    Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X, Pos.Y, Pos.Z, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
                }
                return true;
            }

            if (invSlot.Itemstack.Equals(Api.World, hotbarSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                bool putBulk = player.Entity.Controls.Sprint;

                int q = GameMath.Min(hotbarSlot.StackSize, putBulk ? BulkTransferQuantity : TransferQuantity, Capacity - TotalStackSize);

                // add to the pile and average item temperatures
                int oldSize = invSlot.Itemstack.StackSize;
                invSlot.Itemstack.StackSize += q;
                if (oldSize + q > 0)
                {
                    float tempPile = invSlot.Itemstack.Collectible.GetTemperature(Api.World, invSlot.Itemstack);
                    float tempAdded = hotbarSlot.Itemstack.Collectible.GetTemperature(Api.World, hotbarSlot.Itemstack);
                    invSlot.Itemstack.Collectible.SetTemperature(Api.World, invSlot.Itemstack, (tempPile * oldSize + tempAdded * q) / (oldSize + q), false);
                }

                if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    hotbarSlot.TakeOut(q);
                    hotbarSlot.OnItemSlotModified(null);
                }

                Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X, Pos.Y, Pos.Z, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);

                MarkDirty();

                Cuboidf[] collBoxes = Api.World.BlockAccessor.GetBlock(Pos).GetCollisionBoxes(Api.World.BlockAccessor, Pos);
                if (collBoxes != null && collBoxes.Length > 0 && CollisionTester.AabbIntersect(collBoxes[0], Pos.X, Pos.Y, Pos.Z, player.Entity.SelectionBox, player.Entity.SidedPos.XYZ))
                {
                    player.Entity.SidedPos.Y += collBoxes[0].Y2 - (player.Entity.SidedPos.Y - (int)player.Entity.SidedPos.Y);
                }

                (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                return true;
            }

            return false;
        }

        public bool TryTakeItem(IPlayer player)
        {
            bool takeBulk = player.Entity.Controls.Sprint;
            int q = GameMath.Min(takeBulk ? BulkTransferQuantity : TransferQuantity, TotalStackSize);

            if (inventory[0]?.Itemstack != null)
            {
                ItemStack stack = inventory[0].TakeOut(q);
                player.InventoryManager.TryGiveItemstack(stack);

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }

            if (TotalStackSize == 0)
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
            }

            Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X, Pos.Y, Pos.Z, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);

            MarkDirty();

            (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
        }



        bool putOrGetItemSingle(ItemSlot ourSlot, IPlayer player, BlockSelection bs)
        {
            isUsingSlot = null;
            if (!ourSlot.Empty && ourSlot.Itemstack.Collectible is IContainedInteractable collIci)
            {
                if (collIci.OnContainedInteractStart(this, ourSlot, player, bs))
                {
                    BlockGroundStorage.IsUsingContainedBlock = true;
                    isUsingSlot = ourSlot;
                    return true;
                }
            }

            ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;
            if (!hotbarSlot.Empty && !inventory.Empty)
            {
                bool layoutEqual = StorageProps.Layout == hotbarSlot.Itemstack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps.Layout;
                if (!layoutEqual) return false;
            }


            lock (inventoryLock)
            {
                if (ourSlot.Empty)
                {
                    if (hotbarSlot.Empty) return false;

                    if (player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        ItemStack stack = hotbarSlot.Itemstack.Clone();
                        stack.StackSize = 1;
                        if (new DummySlot(stack).TryPutInto(Api.World, ourSlot, 1) > 0) {
                            Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X, Pos.Y, Pos.Z, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
                        }
                    } else {
                        if (hotbarSlot.TryPutInto(Api.World, ourSlot, 1) > 0)
                        {
                            Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X, Pos.Y, Pos.Z, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
                        }
                    }
                }
                else
                {
                    if (!player.InventoryManager.TryGiveItemstack(ourSlot.Itemstack, true))
                    {
                        Api.World.SpawnItemEntity(ourSlot.Itemstack, new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5));
                    }

                    Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X, Pos.Y, Pos.Z, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);

                    ourSlot.Itemstack = null;
                    ourSlot.MarkDirty();
                }
            }

            return true;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            forceStorageProps = tree.GetBool("forceStorageProps");
            if (forceStorageProps)
            {
                StorageProps = JsonUtil.FromString<GroundStorageProperties>(tree.GetString("storageProps"));
            }

            overrideLayout = null;
            if (tree.HasAttribute("overrideLayout"))
            {
                overrideLayout = (EnumGroundStorageLayout)tree.GetInt("overrideLayout");
            }

            if (this.Api != null)
            {
                DetermineStorageProperties(null);
            }

            if (worldForResolving.Side == EnumAppSide.Client && Api != null)
            {
                updateMeshes();
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("forceStorageProps", forceStorageProps);
            if (forceStorageProps)
            {
                tree.SetString("storageProps", JsonUtil.ToString(StorageProps));
            }
            if (overrideLayout != null)
            {
                tree.SetInt("overrideLayout", (int)overrideLayout);
            }
        }


        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            // Handled by block.GetDrops()
            /*if (Api.World.Side == EnumAppSide.Server)
            {
                inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5), 4);
            }*/
        }



        public virtual string GetBlockName()
        {
            var props = StorageProps;
            if (props == null || inventory.Empty) return "Empty pile";

            string[] contentSummary = getContentSummary();
            if (contentSummary.Length == 1)
            {
                var firstSlot = inventory.FirstNonEmptySlot;

                ItemStack stack = firstSlot.Itemstack;
                int sumQ = inventory.Sum(s => s.StackSize);

                if (firstSlot.Itemstack.Collectible is IContainedCustomName ccn)
                {
                    string name = ccn.GetContainedName(firstSlot, sumQ);
                    if (name != null) return name;
                }


                if (sumQ == 1) return stack.GetName();
                return contentSummary[0];
            }

            return Lang.Get("Ground Storage");
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (inventory.Empty) return;

            string[] contentSummary = getContentSummary();

            ItemStack stack = inventory.FirstNonEmptySlot.Itemstack;
            // Only add supplemental info for non-BlockEntities (otherwise it will be wrong or will get into a recursive loop, because right now this BEGroundStorage is the BlockEntity)
            if (contentSummary.Length == 1 && !(stack.Collectible is IContainedCustomName) && stack.Class == EnumItemClass.Block && ((Block)stack.Collectible).EntityClass == null)  
            {
                string detailedInfo = stack.Block.GetPlacedBlockInfo(Api.World, Pos, forPlayer);
                if (detailedInfo != null && detailedInfo.Length > 0) dsc.Append(detailedInfo);
            } else
            {
                foreach (var line in contentSummary) dsc.AppendLine(line);
            }
        }

        public virtual string[] getContentSummary()
        {
            OrderedDictionary<string, int> dict = new OrderedDictionary<string, int>();

            foreach (var slot in inventory)
            {
                if (slot.Empty) continue;
                int cnt;

                string stackName = slot.Itemstack.GetName();

                if (slot.Itemstack.Collectible is IContainedCustomName ccn)
                {
                    stackName = ccn.GetContainedInfo(slot);
                }

                if (!dict.TryGetValue(stackName, out cnt)) cnt = 0;

                dict[stackName] = cnt + slot.StackSize;
            }

            return dict.Select(elem => Lang.Get("{0}x {1}", elem.Value, elem.Key)).ToArray();
        }



        public override bool OnTesselation(ITerrainMeshPool meshdata, ITesselatorAPI tesselator)
        {
            if (StorageProps == null) return false;

            lock (inventoryLock)
            {
                meshdata.AddMeshData(meshes[0]);

                switch (StorageProps.Layout)
                {
                    case EnumGroundStorageLayout.Halves:
                        // Right
                        meshdata.AddMeshData(meshes[1]);
                        return false;

                    case EnumGroundStorageLayout.Quadrants:
                        // Top right
                        meshdata.AddMeshData(meshes[1]);
                        // Bot left
                        meshdata.AddMeshData(meshes[2]);
                        // Bot right
                        meshdata.AddMeshData(meshes[3]);
                        return false;

                }
            }

            return true;
        }

        
        public override void updateMeshes()
        {
            if (Api.Side == EnumAppSide.Server) return;
            if (StorageProps == null) return;

            lock (inventoryLock)
            {
                switch (StorageProps.Layout)
                {
                    case EnumGroundStorageLayout.SingleCenter:
                        if (inventory[0].Itemstack == null) return;
                        meshes[0] = getOrCreateMesh(inventory[0]);
                        return;

                    case EnumGroundStorageLayout.Halves:
                        // Left
                        meshes[0] = getOrCreateMesh(inventory[0], new Vec3f(-0.25f, 0, 0));
                        // Right
                        meshes[1] = getOrCreateMesh(inventory[1], new Vec3f(0.25f, 0, 0));
                        return;

                    case EnumGroundStorageLayout.Quadrants:
                        // Top left
                        meshes[0] = getOrCreateMesh(inventory[0], new Vec3f(-0.25f, 0, -0.25f));
                        // Top right
                        meshes[1] = getOrCreateMesh(inventory[1], new Vec3f(-0.25f, 0, 0.25f));
                        // Bot left
                        meshes[2] = getOrCreateMesh(inventory[2], new Vec3f(0.25f, 0, -0.25f));
                        // Bot right
                        meshes[3] = getOrCreateMesh(inventory[3], new Vec3f(0.25f, 0, 0.25f));
                        return;

                    case EnumGroundStorageLayout.Stacking:
                        meshes[0] = getOrCreateMesh(inventory[0], null);
                        return;
                }
            }
        }

        

        MeshData getOrCreateMesh(ItemSlot itemSlot, Vec3f offset = null)
        {
            if (itemSlot.Empty) return null;
            if (offset == null) offset = Vec3f.Zero;

            Dictionary<string, MeshData> gmeshes = ObjectCacheUtil.GetOrCreate(capi, "groundStorageMeshes", () => new Dictionary<string, MeshData>());

            string key = StorageProps.Layout + (StorageProps.TessQuantityElements > 0 ? itemSlot.StackSize : 1) + "x" + itemSlot.Itemstack.Collectible.Code.ToShortString() + offset;

            if (itemSlot.Itemstack.Collectible is IContainedMeshSource ics)
            {
                key = ics.GetMeshCacheKey(itemSlot.Itemstack) + StorageProps.Layout + offset;
            }

            MeshData mesh;
            if (gmeshes.TryGetValue(key, out mesh)) return mesh;


            nowTesselatingObj = itemSlot.Itemstack.Collectible;
            ITesselatorAPI mesher = capi.Tesselator;

            if (StorageProps.Layout == EnumGroundStorageLayout.Stacking)
            {
                var loc = StorageProps.StackingModel.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
                nowTesselatingShape = capi.Assets.TryGet(loc)?.ToObject<Shape>();
                if (nowTesselatingShape == null)
                {
                    capi.Logger.Error("Stacking model shape for collectible " + itemSlot.Itemstack.Collectible.Code + " not found. Block will be invisible!");
                    return null;
                }

                mesher.TesselateShape("storagePile", nowTesselatingShape, out mesh, this, null, 0, 0, 0, StorageProps.TessQuantityElements * itemSlot.StackSize);
            }
            else
            {
                mesh = genMesh(itemSlot.Itemstack);
            }

            mesh.Translate(offset);

            gmeshes[key] = mesh;
            return mesh;
        }
        


        public bool TryFire()
        {
            foreach (var slot in inventory)
            {
                if (slot.Empty) continue;

            }

            return true;
        }
    }
}
