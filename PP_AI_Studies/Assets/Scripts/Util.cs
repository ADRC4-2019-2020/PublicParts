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

/// <summary>
/// ClockwiseComparer provides functionality for sorting a collection of Vector3s such
/// that they are ordered clockwise about a given origin.
/// Copied and addpated from https://pastebin.com/1RkaP28U 
/// </summary>
public class ClockwiseComparer : IComparer<Vector3Int>
{
	private Vector3 m_Origin;

	#region Properties

	/// <summary>
	/// Gets or sets the origin.
	/// </summary>
	/// <value>The origin.</value>
	public Vector3 origin { get { return m_Origin; } set { m_Origin = value; } }

	#endregion

	/// <summary>
	/// Initializes a new instance of the ClockwiseComparer class.
	/// </summary>
	/// <param name="origin">Origin.</param>
	public ClockwiseComparer(Vector3 origin)
	{
		m_Origin = origin;
	}

	#region IComparer Methods

	/// <summary>
	/// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
	/// </summary>
	/// <param name="first">First.</param>
	/// <param name="second">Second.</param>
	public int Compare(Vector3Int first, Vector3Int second)
	{
		return IsClockwise(first, second, m_Origin);
	}

	#endregion

	/// <summary>
	/// 	Returns 1 if first comes before second in clockwise order.
	/// 	Returns -1 if second comes before first.
	/// 	Returns 0 if the points are identical.
	/// </summary>
	/// <param name="first">First.</param>
	/// <param name="second">Second.</param>
	/// <param name="origin">Origin.</param>
	public static int IsClockwise(Vector3Int first, Vector3Int second, Vector3 origin)
	{
		if (first == second)
			return 0;
        //Vector3 approxOrigin = new Vector3Int(Mathf.RoundToInt(origin.x), Mathf.RoundToInt(origin.y), Mathf.RoundToInt(origin.z));
        
        Vector3 firstOffset = first - origin;
		Vector3 secondOffset = second - origin;

		float angle1 = Mathf.Atan2(firstOffset.x, firstOffset.z);
		float angle2 = Mathf.Atan2(secondOffset.x, secondOffset.z);

		if (angle1 < angle2)
			return -1;

		if (angle1 > angle2)
			return 1;

		// Check to see which point is closest
		return (firstOffset.sqrMagnitude < secondOffset.sqrMagnitude) ? -1 : 1;
	}
}

public class ClockwiseVector3Comparer : IComparer<Vector3Int>
{
    public int Compare(Vector3Int v1, Vector3Int v2)
    {
        return Mathf.Atan2(v1.x, v1.z).CompareTo(Mathf.Atan2(v2.x, v2.z));
    }
}