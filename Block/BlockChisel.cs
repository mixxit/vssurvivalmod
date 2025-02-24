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
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class BlockChisel : BlockMicroBlock
    {

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if ((inSlot.Itemstack.Attributes["materials"] as StringArrayAttribute)?.value.Length > 1 || (inSlot.Itemstack.Attributes["materials"] as IntArrayAttribute)?.value.Length > 1)
            {
                dsc.AppendLine(Lang.Get("<font color=\"lightblue\">Multimaterial chiseled block</a>"));
            }
        }


    }
}
