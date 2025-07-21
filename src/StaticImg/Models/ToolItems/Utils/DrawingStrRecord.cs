using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Workloads.Creation.StaticImg.Models.ToolItems.Utils.Base;

namespace Workloads.Creation.StaticImg.Models.ToolItems.Utils
{
    internal record DrawingStrRecord(float X, float Y, Color old) : StructuredRecord {
    }
}
