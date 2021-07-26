﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumCropStressType
    {
        Unknown = 0,
        TooHot = 1,
        TooCold = 2,
        /*Disease,
        TooDry,
        TooWet*/
    }

    public class BlockEntityDeadCrop : BlockEntityContainer
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

         

        public override string InventoryClassName => "deadcrop";

        public EnumCropStressType deathReason;


        public BlockEntityDeadCrop()
        {
            inv = new InventoryGeneric(1, "deadcrop-0", null, null);
        }


        public ItemStack[] GetDrops(IPlayer byPlayer, float dropQuantityMultiplier)
        {
            if (inv[0].Empty) return new ItemStack[0];
            return inv[0].Itemstack.Block.GetDrops(Api.World, Pos, byPlayer, dropQuantityMultiplier);
        }

        public string GetPlacedBlockName()
        {
            if (inv[0].Empty) return Lang.Get("Dead crop");
            return Lang.Get("Dead {0}", inv[0].Itemstack.GetName());
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            deathReason = (EnumCropStressType)tree.GetInt("deathReason", 0);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("deathReason", (int)deathReason);
        }

        public override void OnBlockBroken()
        {
            //base.OnBlockBroken(); - dont drop contents
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            switch (deathReason)
            {
                case EnumCropStressType.TooHot: dsc.AppendLine(Lang.Get("Died from too high temperatues.")); break;
                case EnumCropStressType.TooCold: dsc.AppendLine(Lang.Get("Died from too low temperatures.")); break;
                //case EnumCropStressType.Disease: dsc.AppendLine(Lang.Get("Died from disease.")); break;
                //case EnumCropStressType.TooDry: dsc.AppendLine(Lang.Get("Died from to little moisture.")); break;
                //case EnumCropStressType.TooWet: dsc.AppendLine(Lang.Get("Died from to much moisture.")); break;
            }

            if (!inv[0].Empty) dsc.Append(inv[0].Itemstack.Block.GetPlacedBlockInfo(Api.World, Pos, forPlayer));
        }

    }
}
 