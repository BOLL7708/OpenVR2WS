using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenVR2WS.Data;
using Valve.VR;

namespace OpenVR2WS.Output
{
    class PlayArea : BaseResponse
    {
        public Vec3[] corners = new Vec3[8];
        public Vec3 size = new Vec3();

        public PlayArea(HmdQuad_t rect = new HmdQuad_t(), HmdVector2_t size = new HmdVector2_t(), float height = 0)
        {
            Update(rect, size, height);
        }

        public void Update(HmdQuad_t rect, HmdVector2_t size, float height)
        {
            corners[0] = new Vec3(rect.vCorners0);
            corners[1] = new Vec3(rect.vCorners1);
            corners[2] = new Vec3(rect.vCorners2);
            corners[3] = new Vec3(rect.vCorners3);
            
            corners[4] = new Vec3(rect.vCorners0);
            corners[5] = new Vec3(rect.vCorners1);
            corners[6] = new Vec3(rect.vCorners2);
            corners[7] = new Vec3(rect.vCorners3);

            corners[4].y = height;
            corners[5].y = height;
            corners[6].y = height;
            corners[7].y = height;

            this.size.x = size.v0;
            this.size.y = height;
            this.size.z = size.v1;
        }
    }
}
