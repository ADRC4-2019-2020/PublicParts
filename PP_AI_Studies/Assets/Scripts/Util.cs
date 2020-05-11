using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public enum Axis { X, Y, Z };
public enum BoundaryType { Inside = 0, Left = -1, Right = 1, Outside = 2 }
public enum BuildingZone { Northwest, Northeast, Southeast, Southwest };
public enum PartType { Structure, Shower, WCSink, Toilet, Laundry, Dumb, Bedroom, KitchenOven, KitchenStove, KitchenSink, KitchenTop, Configurable, Knot };
public enum PartOrientation { Vertical, Horizontal, Agnostic };
public enum SpaceFunction { Work, Leisure };

public class ParseVector3 : MonoBehaviour
{
    public int X;
    public int Y;
    public int Z;

    public Vector3Int ToVector3Int()
    {
        return new Vector3Int(X, Y, Z);
    }
}

static class Util
{
    public static Vector3 Average(this IEnumerable<Vector3> vectors)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (var vector in vectors)
        {
            sum += vector;
            count++;
        }

        sum /= count;
        return sum;
    }

    public static T MinBy<T>(this IEnumerable<T> items, Func<T, double> selector)
    {
        double minValue = double.MaxValue;
        T minItem = items.First();

        foreach (var item in items)
        {
            var value = selector(item);

            if (value < minValue)
            {
                minValue = value;
                minItem = item;
            }
        }

        return minItem;
    }

    public static T MaxBy<T>(this IEnumerable<T> items, Func<T, double> selector)
    {
        double maxValue = double.MinValue;
        T maxItem = items.First();

        foreach (var item in items)
        {
            var value = selector(item);

            if (value > maxValue)
            {
                maxValue = value;
                maxItem = item;
            }
        }

        return maxItem;
    }
}