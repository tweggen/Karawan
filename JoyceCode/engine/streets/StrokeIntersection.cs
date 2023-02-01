using System.Numerics;

namespace engine.streets
{
    public class StrokeIntersection
    {
        public Vector2 Pos;
        public StreetPoint? StreetPoint;

        public Stroke? StrokeCand;
        public float ScaleCand;

        public Stroke StrokeExists;
        public float ScaleExists;


        public StrokeIntersection(
            in Vector2 pos,
            in StreetPoint streetPoint,
            in Stroke strokeExists,
            float scaleExists)
        {
            Pos = pos;
            StreetPoint = streetPoint;
            StrokeExists = strokeExists;
            ScaleExists = scaleExists;
        }

        public StrokeIntersection(
            in Vector2 pos,
            in Stroke strokeCand,
            float scaleCand,
            in Stroke strokeExists,
            float scaleExists
            ) 
        {
            Pos = pos;
            StreetPoint = null;
            StrokeCand = strokeCand;
            ScaleCand = scaleCand;
            StrokeExists = strokeExists;
            ScaleExists = scaleExists;
        }
    }
}
