using System.Numerics;

namespace SiegeEngine.PlayerSystem
{
    public class PositionCalculator
    {
        private const int BackgroundWidth = 1920;
        private const int BackgroundHeight = 895;

        public Vector2 CalculateAdjustedPosition(Vector2 position, string positioningMode, int windowWidth, int windowHeight)
        {
            Vector2 adjustedPos = position;
            if (positioningMode == "centerOffset")
            {
                adjustedPos = new Vector2(
                    windowWidth / 2.0f + position.X,
                    windowHeight / 2.0f + position.Y
                );
            }
            else if (positioningMode == "backgroundPercentage")
            {
                float backgroundX = position.X * BackgroundWidth;
                float backgroundY = position.Y * BackgroundHeight;

                float offsetX = (windowWidth - BackgroundWidth) / 2.0f;
                float offsetY = (windowHeight - BackgroundHeight) / 2.0f;

                adjustedPos = new Vector2(
                    offsetX + BackgroundWidth / 2.0f + backgroundX,
                    offsetY + BackgroundHeight / 2.0f + backgroundY
                );
            }
            return adjustedPos;
        }
    }
}