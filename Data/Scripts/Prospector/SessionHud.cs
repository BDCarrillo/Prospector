using Draygo.API;
using System.Text;
using VRageMath;

namespace Prospector
{
    public partial class Session
    {
        private void HudRegisterObjects()
        {
            var s = Settings.Instance;
            var sizeMult = 0.75f;
            var topRightDraw = new Vector2D(ctrOffset, ctrOffset);
            var topLeftDraw = new Vector2D(-ctrOffset, ctrOffset);
            var botRightDraw = new Vector2D(ctrOffset, -ctrOffset);
            var botLeftDraw = new Vector2D(-ctrOffset, -ctrOffset);
            var midLeftDraw = new Vector2D(-ctrOffset, 0);
            topLeft = new HudAPIv2.BillBoardHUDMessage(frameCorner, topLeftDraw, s.expandedColor, Width: symbolWidth * sizeMult, Height: symbolHeight * sizeMult, Rotation: 0, HideHud: true, Shadowing: true);
            topRight = new HudAPIv2.BillBoardHUDMessage(frameCorner, topRightDraw, s.expandedColor, Width: symbolWidth * sizeMult, Height: symbolHeight * sizeMult, Rotation: 1.5708f, HideHud: true, Shadowing: true);
            botRight = new HudAPIv2.BillBoardHUDMessage(frameCorner, botRightDraw, s.expandedColor, Width: symbolWidth * sizeMult, Height: symbolHeight * sizeMult, Rotation: 3.14159f, HideHud: true, Shadowing: true);
            botLeft = new HudAPIv2.BillBoardHUDMessage(frameCorner, botLeftDraw, s.expandedColor, Width: symbolWidth * sizeMult, Height: symbolHeight * sizeMult, Rotation: -1.5708f, HideHud: true, Shadowing: true);
            scanRing = new HudAPIv2.BillBoardHUDMessage(hollowCircle, Vector2D.Zero, Color.White, Width: symbolWidth * 1.5f, Height: symbolHeight * 1.5f, HideHud: true, Shadowing: true);
            texture = new HudAPIv2.BillBoardHUDMessage(scannerTexture, Vector2D.Zero, s.expandedColor, Width: (float)ctrOffset*2 + symbolWidth * sizeMult, Height: (float)ctrOffset*2 + symbolHeight * sizeMult, HideHud: true, Blend: VRageRender.MyBillboard.BlendTypeEnum.AdditiveTop);
            scanLine = new HudAPIv2.BillBoardHUDMessage(scanLineTexture, midLeftDraw, s.expandedColor, Width: symbolHeight * 0.05f, Height: (float)ctrOffset * 2 + symbolHeight * sizeMult, HideHud: true, Blend: VRageRender.MyBillboard.BlendTypeEnum.AdditiveTop);
            message = new HudAPIv2.HUDMessage(new StringBuilder(), topRightDraw, new Vector2D(.01, .025), -1, 1, true, true);
            message.InitialColor = s.expandedColor;
            title = new HudAPIv2.HUDMessage(new StringBuilder("Data Review Mode - Scanning Suspended"), topLeftDraw, new Vector2D(-.01, .055), -1, 1.15, true, true);
            title.InitialColor = s.expandedColor;

            title.Visible = false;
            message.Visible = false;
            topLeft.Visible = false;
            topRight.Visible = false;
            botRight.Visible = false;
            botLeft.Visible = false;
            scanRing.Visible = false;
            texture.Visible = false;
            scanLine.Visible = false;
            hudObjectsRegistered = true;
        }
        private void HudUpdateColor()
        {
            var s = Settings.Instance;
            topLeft.BillBoardColor = s.expandedColor;
            topRight.BillBoardColor = s.expandedColor;
            botLeft.BillBoardColor = s.expandedColor;
            botRight.BillBoardColor = s.expandedColor;
            title.InitialColor = s.expandedColor;
            message.InitialColor = s.expandedColor;
            texture.BillBoardColor = s.expandedColor;
            scanLine.BillBoardColor = s.expandedColor;
        }

        private void HudCycleVisibility(bool visible)
        {
            scanRing.Visible = !visible;
            title.Visible = visible;
            message.Visible = visible;
            topLeft.Visible = visible;
            topRight.Visible = visible;
            botRight.Visible = visible;
            botLeft.Visible = visible;
            texture.Visible = visible;
            scanLine.Visible = visible;
            if (!visible)
                scanLine.Offset = Vector2D.Zero;
        }
    }
}
