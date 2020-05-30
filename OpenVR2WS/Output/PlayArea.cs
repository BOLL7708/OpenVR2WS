using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;

namespace OpenVR2WS.Output
{
    class PlayArea
    {
        public Vec2 cornerTopLeft = new Vec2();
        public Vec2 cornerBottomRight = new Vec2();
        public Vec2 size = new Vec2();

        public PlayArea(HmdQuad_t rect = new HmdQuad_t(), HmdVector2_t size = new HmdVector2_t())
        {
            Update(rect, size);
        }

        public void Update(HmdQuad_t rect, HmdVector2_t size)
        {
            cornerTopLeft.x = rect.vCorners1.v0;
            cornerTopLeft.y = rect.vCorners1.v2;
            cornerBottomRight.x = rect.vCorners3.v0;
            cornerBottomRight.y = rect.vCorners3.v2;
            this.size.x = size.v0;
            this.size.y = size.v1;
        }
    }
}
