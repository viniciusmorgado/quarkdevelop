//
// Primitives.cs
//
// Copyright (c) 2026 MonoDevelop contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Globalization;

namespace System.Windows
{
	/// <summary>A point in device-independent units (WPF semantics).</summary>
	public struct Point : IEquatable<Point>
	{
		public Point (double x, double y)
		{
			X = x;
			Y = y;
		}

		public double X { get; set; }

		public double Y { get; set; }

		public void Offset (double offsetX, double offsetY)
		{
			X += offsetX;
			Y += offsetY;
		}

		public static Point operator + (Point point, Vector vector) => new Point (point.X + vector.X, point.Y + vector.Y);

		public static Point operator - (Point point, Vector vector) => new Point (point.X - vector.X, point.Y - vector.Y);

		public static Vector operator - (Point point1, Point point2) => new Vector (point1.X - point2.X, point1.Y - point2.Y);

		public static bool operator == (Point point1, Point point2) => point1.Equals (point2);

		public static bool operator != (Point point1, Point point2) => !point1.Equals (point2);

		public bool Equals (Point other) => X.Equals (other.X) && Y.Equals (other.Y);

		public override bool Equals (object obj) => obj is Point other && Equals (other);

		public override int GetHashCode () => HashCode.Combine (X, Y);

		public override string ToString () => string.Format (CultureInfo.InvariantCulture, "{0},{1}", X, Y);
	}

	/// <summary>A displacement (WPF semantics).</summary>
	public struct Vector : IEquatable<Vector>
	{
		public Vector (double x, double y)
		{
			X = x;
			Y = y;
		}

		public double X { get; set; }

		public double Y { get; set; }

		public double Length => Math.Sqrt (X * X + Y * Y);

		public static Vector operator + (Vector vector1, Vector vector2) => new Vector (vector1.X + vector2.X, vector1.Y + vector2.Y);

		public static Vector operator - (Vector vector) => new Vector (-vector.X, -vector.Y);

		public static bool operator == (Vector vector1, Vector vector2) => vector1.Equals (vector2);

		public static bool operator != (Vector vector1, Vector vector2) => !vector1.Equals (vector2);

		public bool Equals (Vector other) => X.Equals (other.X) && Y.Equals (other.Y);

		public override bool Equals (object obj) => obj is Vector other && Equals (other);

		public override int GetHashCode () => HashCode.Combine (X, Y);

		public override string ToString () => string.Format (CultureInfo.InvariantCulture, "{0},{1}", X, Y);
	}

	/// <summary>A width and height; <see cref="Empty"/> has negative infinite dimensions (WPF semantics).</summary>
	public struct Size : IEquatable<Size>
	{
		double width, height;

		public Size (double width, double height)
		{
			if (width < 0 || height < 0)
				throw new ArgumentException ("Width and Height must be non-negative.");
			this.width = width;
			this.height = height;
		}

		public static Size Empty { get; } = new Size { width = double.NegativeInfinity, height = double.NegativeInfinity };

		public bool IsEmpty => width < 0;

		public double Width {
			get => width;
			set {
				if (IsEmpty)
					throw new InvalidOperationException ("Cannot modify an empty Size.");
				if (value < 0)
					throw new ArgumentException ("Width must be non-negative.");
				width = value;
			}
		}

		public double Height {
			get => height;
			set {
				if (IsEmpty)
					throw new InvalidOperationException ("Cannot modify an empty Size.");
				if (value < 0)
					throw new ArgumentException ("Height must be non-negative.");
				height = value;
			}
		}

		public static bool operator == (Size size1, Size size2) => size1.Equals (size2);

		public static bool operator != (Size size1, Size size2) => !size1.Equals (size2);

		public bool Equals (Size other) => width.Equals (other.width) && height.Equals (other.height);

		public override bool Equals (object obj) => obj is Size other && Equals (other);

		public override int GetHashCode () => HashCode.Combine (width, height);

		public override string ToString () => IsEmpty ? "Empty" : string.Format (CultureInfo.InvariantCulture, "{0},{1}", width, height);
	}

	/// <summary>An axis-aligned rectangle; <see cref="Empty"/> is at +infinity with negative infinite size (WPF semantics).</summary>
	public struct Rect : IEquatable<Rect>
	{
		double x, y, width, height;

		public Rect (double x, double y, double width, double height)
		{
			if (width < 0 || height < 0)
				throw new ArgumentException ("Width and Height must be non-negative.");
			this.x = x;
			this.y = y;
			this.width = width;
			this.height = height;
		}

		public Rect (Point location, Size size)
		{
			if (size.IsEmpty) {
				this = Empty;
				return;
			}
			x = location.X;
			y = location.Y;
			width = size.Width;
			height = size.Height;
		}

		public Rect (Point point1, Point point2)
		{
			x = Math.Min (point1.X, point2.X);
			y = Math.Min (point1.Y, point2.Y);
			width = Math.Abs (point2.X - point1.X);
			height = Math.Abs (point2.Y - point1.Y);
		}

		public Rect (Size size) : this (new Point (), size)
		{
		}

		public static Rect Empty { get; } = new Rect {
			x = double.PositiveInfinity, y = double.PositiveInfinity,
			width = double.NegativeInfinity, height = double.NegativeInfinity
		};

		public bool IsEmpty => width < 0;

		public double X { get => x; set { ThrowIfEmpty (); x = value; } }

		public double Y { get => y; set { ThrowIfEmpty (); y = value; } }

		public double Width {
			get => width;
			set {
				ThrowIfEmpty ();
				if (value < 0)
					throw new ArgumentException ("Width must be non-negative.");
				width = value;
			}
		}

		public double Height {
			get => height;
			set {
				ThrowIfEmpty ();
				if (value < 0)
					throw new ArgumentException ("Height must be non-negative.");
				height = value;
			}
		}

		public double Left => x;

		public double Top => y;

		public double Right => IsEmpty ? double.NegativeInfinity : x + width;

		public double Bottom => IsEmpty ? double.NegativeInfinity : y + height;

		public Point Location {
			get => new Point (x, y);
			set { ThrowIfEmpty (); x = value.X; y = value.Y; }
		}

		public Size Size {
			get => IsEmpty ? Size.Empty : new Size (width, height);
			set {
				if (value.IsEmpty) {
					this = Empty;
				} else {
					ThrowIfEmpty ();
					width = value.Width;
					height = value.Height;
				}
			}
		}

		public Point TopLeft => new Point (Left, Top);

		public Point TopRight => new Point (Right, Top);

		public Point BottomLeft => new Point (Left, Bottom);

		public Point BottomRight => new Point (Right, Bottom);

		public bool Contains (double px, double py) => !IsEmpty && px >= x && px - width <= x && py >= y && py - height <= y;

		public bool Contains (Point point) => Contains (point.X, point.Y);

		public bool Contains (Rect rect) => !IsEmpty && !rect.IsEmpty && x <= rect.x && y <= rect.y && x + width >= rect.x + rect.width && y + height >= rect.y + rect.height;

		public bool IntersectsWith (Rect rect) =>
			!IsEmpty && !rect.IsEmpty && rect.Left <= Right && rect.Right >= Left && rect.Top <= Bottom && rect.Bottom >= Top;

		public void Intersect (Rect rect)
		{
			if (!IntersectsWith (rect)) {
				this = Empty;
				return;
			}
			double left = Math.Max (Left, rect.Left);
			double top = Math.Max (Top, rect.Top);
			width = Math.Max (Math.Min (Right, rect.Right) - left, 0);
			height = Math.Max (Math.Min (Bottom, rect.Bottom) - top, 0);
			x = left;
			y = top;
		}

		public static Rect Intersect (Rect rect1, Rect rect2)
		{
			rect1.Intersect (rect2);
			return rect1;
		}

		public void Union (Rect rect)
		{
			if (IsEmpty) {
				this = rect;
			} else if (!rect.IsEmpty) {
				double left = Math.Min (Left, rect.Left);
				double top = Math.Min (Top, rect.Top);
				width = Math.Max (Right, rect.Right) - left;
				height = Math.Max (Bottom, rect.Bottom) - top;
				x = left;
				y = top;
			}
		}

		public static Rect Union (Rect rect1, Rect rect2)
		{
			rect1.Union (rect2);
			return rect1;
		}

		public void Offset (double offsetX, double offsetY)
		{
			ThrowIfEmpty ();
			x += offsetX;
			y += offsetY;
		}

		public void Offset (Vector offsetVector) => Offset (offsetVector.X, offsetVector.Y);

		public void Inflate (double widthAmount, double heightAmount)
		{
			ThrowIfEmpty ();
			x -= widthAmount;
			y -= heightAmount;
			width += 2 * widthAmount;
			height += 2 * heightAmount;
			if (width < 0 || height < 0)
				this = Empty;
		}

		public static Rect Inflate (Rect rect, double width, double height)
		{
			rect.Inflate (width, height);
			return rect;
		}

		void ThrowIfEmpty ()
		{
			if (IsEmpty)
				throw new InvalidOperationException ("Cannot modify an empty Rect.");
		}

		public static bool operator == (Rect rect1, Rect rect2) => rect1.Equals (rect2);

		public static bool operator != (Rect rect1, Rect rect2) => !rect1.Equals (rect2);

		public bool Equals (Rect other) => IsEmpty ? other.IsEmpty :
			x.Equals (other.x) && y.Equals (other.y) && width.Equals (other.width) && height.Equals (other.height);

		public override bool Equals (object obj) => obj is Rect other && Equals (other);

		public override int GetHashCode () => IsEmpty ? 0 : HashCode.Combine (x, y, width, height);

		public override string ToString () => IsEmpty ? "Empty" : string.Format (CultureInfo.InvariantCulture, "{0},{1},{2},{3}", x, y, width, height);
	}

	/// <summary>Uniform or per-side lengths around a rectangle.</summary>
	public struct Thickness : IEquatable<Thickness>
	{
		public Thickness (double uniformLength) : this (uniformLength, uniformLength, uniformLength, uniformLength)
		{
		}

		public Thickness (double left, double top, double right, double bottom)
		{
			Left = left;
			Top = top;
			Right = right;
			Bottom = bottom;
		}

		public double Left { get; set; }

		public double Top { get; set; }

		public double Right { get; set; }

		public double Bottom { get; set; }

		public static bool operator == (Thickness t1, Thickness t2) => t1.Equals (t2);

		public static bool operator != (Thickness t1, Thickness t2) => !t1.Equals (t2);

		public bool Equals (Thickness other) => Left.Equals (other.Left) && Top.Equals (other.Top) && Right.Equals (other.Right) && Bottom.Equals (other.Bottom);

		public override bool Equals (object obj) => obj is Thickness other && Equals (other);

		public override int GetHashCode () => HashCode.Combine (Left, Top, Right, Bottom);
	}

	/// <summary>
	/// Placeholder for WPF visual elements in editor contracts (adornments, space reservation). The GTK
	/// editor passes its own objects through these APIs; there is no WPF layout here.
	/// </summary>
	public class UIElement
	{
	}
}

namespace System.Windows.Media
{
	/// <summary>
	/// Placeholder for WPF images in the legacy completion and glyph contracts; the GTK editor uses its
	/// own image types and never creates one.
	/// </summary>
	public abstract class ImageSource
	{
		public virtual double Width => 0;

		public virtual double Height => 0;
	}

	/// <summary>A shape with bounds; the editor only uses geometries as regions.</summary>
	public abstract class Geometry
	{
		public abstract Rect Bounds { get; }

		public virtual bool IsEmpty () => Bounds.IsEmpty;

		public virtual bool FillContains (Point hitPoint) => Bounds.Contains (hitPoint);
	}

	public sealed class RectangleGeometry : Geometry
	{
		public RectangleGeometry ()
		{
		}

		public RectangleGeometry (Rect rect)
		{
			Rect = rect;
		}

		public Rect Rect { get; set; }

		public override Rect Bounds => Rect;
	}

	public sealed class GeometryGroup : Geometry
	{
		public System.Collections.Generic.List<Geometry> Children { get; } = new System.Collections.Generic.List<Geometry> ();

		public override Rect Bounds {
			get {
				var bounds = Rect.Empty;
				foreach (var child in Children)
					bounds.Union (child.Bounds);
				return bounds;
			}
		}

		public override bool FillContains (Point hitPoint)
		{
			foreach (var child in Children) {
				if (child.FillContains (hitPoint))
					return true;
			}
			return false;
		}
	}
}
