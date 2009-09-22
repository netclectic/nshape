﻿/******************************************************************************
  Copyright 2009 dataweb GmbH
  This file is part of the nShape framework.
  nShape is free software: you can redistribute it and/or modify it under the 
  terms of the GNU General Public License as published by the Free Software 
  Foundation, either version 3 of the License, or (at your option) any later 
  version.
  nShape is distributed in the hope that it will be useful, but WITHOUT ANY
  WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR 
  A PARTICULAR PURPOSE.  See the GNU General Public License for more details.
  You should have received a copy of the GNU General Public License along with 
  nShape. If not, see <http://www.gnu.org/licenses/>.
******************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;


namespace Dataweb.NShape.Advanced {

	/// <summary>
	/// Manages drawing tools for GDI+.
	/// </summary>
	public static class ToolCache {

		#region [Public] Methods

		/// <summary>
		/// Releases all resources
		/// </summary>
		public static void Clear() {
			// dispose and clear pens
			foreach (KeyValuePair<PenKey, Pen> item in penCache)
				item.Value.Dispose();
			penCache.Clear();

			// dispose and clear solid brushes
			foreach (KeyValuePair<IColorStyle, SolidBrush> item in solidBrushCache)
				item.Value.Dispose();
			solidBrushCache.Clear();

			// dispose and clear other brushes
			foreach (KeyValuePair<BrushKey, Brush> item in brushCache)
				item.Value.Dispose();
			brushCache.Clear();

			// dispose and clear image attributes
			foreach (KeyValuePair<BrushKey, ImageAttributes> item in imageAttribsCache)
				item.Value.Dispose();
			imageAttribsCache.Clear();

			// dispose and clear fonts
			foreach (KeyValuePair<ICharacterStyle, Font> item in fontCache)
				item.Value.Dispose();
			fontCache.Clear();

			// dispose and clear string formatters
			foreach (KeyValuePair<IParagraphStyle, StringFormat> item in stringFormatCache)
				item.Value.Dispose();
			stringFormatCache.Clear();

			// dispose and clear cap paths
			foreach (KeyValuePair<CapKey, GraphicsPath> item in capPathCache)
				item.Value.Dispose();
			capPathCache.Clear();

			// dispose and clear caps
			foreach (KeyValuePair<CapKey, CustomLineCap> item in capCache)
				item.Value.Dispose();
			capCache.Clear();
		}


		/// <summary>
		/// Releases resources used for styles of the the given StyleSet
		/// </summary>
		public static void RemoveStyleSetTools(IStyleSet styleSet) {
			if (styleSet == null) throw new ArgumentNullException("styleSet");

			// delete GDI+ objects created from styles
			foreach (ICapStyle style in styleSet.CapStyles)
				ToolCache.NotifyCapStyleChanged(style);
			foreach (ICharacterStyle style in styleSet.CharacterStyles)
				ToolCache.NotifyCharacterStyleChanged(style);
			foreach (IColorStyle style in styleSet.ColorStyles)
				ToolCache.NotifyColorStyleChanged(style);
			foreach (IFillStyle style in styleSet.FillStyles)
				ToolCache.NotifyFillStyleChanged(style);
			foreach (ILineStyle style in styleSet.LineStyles)
				ToolCache.NotifyLineStyleChanged(style);
			foreach (IParagraphStyle style in styleSet.ParagraphStyles)
				ToolCache.NotifyParagraphStyleChanged(style);
			//foreach (IShapeStyle style in styleSet.ShapeStyles)
			//   ToolCache.NotifyShapeStyleChanged(style);
		}


		public static void NotifyStyleChanged(IStyle style) {
			if (style == null) throw new ArgumentNullException("style");
			if (style is ICapStyle) NotifyCapStyleChanged((ICapStyle)style);
			else if (style is ICharacterStyle) NotifyCharacterStyleChanged((ICharacterStyle)style);
			else if (style is IColorStyle) NotifyColorStyleChanged((IColorStyle)style);
			else if (style is IFillStyle) NotifyFillStyleChanged((IFillStyle)style);
			else if (style is ILineStyle) NotifyLineStyleChanged((ILineStyle)style);
			else if (style is IParagraphStyle) NotifyParagraphStyleChanged((IParagraphStyle)style);
			else if (style is IShapeStyle) NotifyShapeStyleChanged((IShapeStyle)style);
			else throw new ArgumentException("style");
		}


		/// <summary>
		/// Finds and returns the brush for the given FillStyle. The brush will be translated, scaled and rotated.
		/// </summary>
		/// <param name="fillStyle">The FillStyle the brush belongs to.</param>
		/// <param name="unrotatedBounds">BoundingRectangle of the unrotated shape.</param>
		/// <param name="shapeAngle">Angle in tenths of degrees.</param>
		/// <returns></returns>
		public static Brush GetTransformedBrush(IFillStyle fillStyle, Rectangle unrotatedBounds, Point center, int angle) {
			if (fillStyle == null) throw new ArgumentNullException("fillStyle");

			Brush brush = GetBrush(fillStyle);
			float angleDeg = Geometry.TenthsOfDegreeToDegrees(angle);
			if (brush is LinearGradientBrush)
				GdiHelpers.TransformLinearGradientBrush((LinearGradientBrush)brush, fillStyle.GradientAngle, unrotatedBounds, center, angleDeg);
			else if (brush is PathGradientBrush)
				GdiHelpers.TransformPathGradientBrush((PathGradientBrush)brush, unrotatedBounds, center, angleDeg);
			else if (brush is TextureBrush)
				GdiHelpers.TransformTextureBrush((TextureBrush)brush, fillStyle.ImageLayout, unrotatedBounds, center, angleDeg);
			return brush;
		}


		public static CustomLineCap GetCustomLineCap(ICapStyle capStyle, ILineStyle lineStyle) {
			if (capStyle == null) throw new ArgumentNullException("capStyle");
			if (lineStyle == null) throw new ArgumentNullException("lineStyle");

			// build CapKey
			CapKey capKey;
			capKey.CapStyle = capStyle;
			capKey.LineStyle = lineStyle;
			// get GraphicsPath for the CustomLineCap
			GraphicsPath capPath = GetCapPath(capStyle, lineStyle);
			// find/create CustomLineCap
			CustomLineCap customCap = null;
			capCache.TryGetValue(capKey, out customCap);
			if (customCap == null) {
				customCap = new CustomLineCap(null, capPath);
				customCap.StrokeJoin = LineJoin.Round;
				customCap.WidthScale = 1;
				rectFBuffer = capPath.GetBounds();
				if (capStyle.CapShape == CapShape.ArrowOpen)
					customCap.BaseInset = 0;
				else 
					customCap.BaseInset = (float)(rectFBuffer.Height - (rectFBuffer.Height + rectFBuffer.Y));
				capCache.Add(capKey, customCap);
			}
			return customCap;
		}


		/// <summary>
		/// Returns the untransformed bounding rectangle of the line cap defined by the given style.
		/// </summary>
		public static Rectangle GetCapBounds(ICapStyle capStyle, ILineStyle lineStyle, float angleDeg) {
			if (capStyle == null) throw new ArgumentNullException("capStyle");
			if (lineStyle == null) throw new ArgumentNullException("lineStyle");
			Rectangle result = Rectangle.Empty;
			GetCapPoints(capStyle, lineStyle, ref pointFBuffer);

			// Transform cap points
			matrix.Reset();
			matrix.RotateAt(angleDeg + 90, Point.Empty);
			// Scale GraphicsPath up for correcting the automatic scaling that is applied to
			// LineCaps by GDI+ when altering the LineWidth of the pen
			matrix.Scale(lineStyle.LineWidth, lineStyle.LineWidth);
			matrix.TransformPoints(pointFBuffer);

			Geometry.CalcBoundingRectangle(pointFBuffer, out result);
			return result;
		}


		public static void GetCapPoints(ICapStyle capStyle, ILineStyle lineStyle, ref PointF[] capPoints) {
			if (capStyle == null) throw new ArgumentNullException("capStyle");
			if (lineStyle == null) throw new ArgumentNullException("lineStyle");
			
			GraphicsPath capPath = GetCapPath(capStyle, lineStyle);
			if (capPoints == null)
				capPoints = new PointF[capPath.PointCount];
			else if (capPoints.Length != capPath.PointCount)
				Array.Resize(ref capPoints, capPath.PointCount);
			Array.Copy(capPath.PathPoints, capPoints, capPoints.Length);
		}

		
		public static GraphicsPath GetCapPath(ICapStyle capStyle, ILineStyle lineStyle) {
			if (capStyle == null) throw new ArgumentNullException("capStyle");
			if (lineStyle == null) throw new ArgumentNullException("lineStyle");

			// build CapKey
			CapKey capKey;
			capKey.CapStyle = capStyle;
			capKey.LineStyle = lineStyle;
			// find/create CapPath
			GraphicsPath capPath;
			capPathCache.TryGetValue(capKey, out capPath);
			if (capPath == null) {
				capPath = new GraphicsPath();
				CalcCapShape(ref capPath, capStyle.CapShape, capStyle.CapSize);
				// Scale GraphicsPath down for correcting the automatic scaling that is applied to
				// LineCaps by GDI+ when altering the LineWidth of the pen
				matrix.Reset();
				matrix.Scale(1f / lineStyle.LineWidth, 1f / lineStyle.LineWidth);
				capPath.Transform(matrix);
				capPathCache.Add(capKey, capPath);
			}
			return capPath;
		}
		

		public static Pen GetPen(ILineStyle lineStyle, ICapStyle startCapStyle, ICapStyle endCapStyle) {
			if (lineStyle == null) throw new ArgumentNullException("lineStyle");
			
			PenKey penKey;
			penKey.LineStyle = lineStyle;
			penKey.StartCapStyle = startCapStyle;
			penKey.EndCapStyle = endCapStyle;

			Pen pen = null;
			if (!penCache.TryGetValue(penKey, out pen)) {
				// If the corresponding pen was not found, create a new pen based on the given LineStyle
				pen = new Pen(GetColor(lineStyle.ColorStyle, lineStyle.ColorStyle.ConvertToGray), lineStyle.LineWidth);
				// does not draw exactly along the outline of the shape and
				// causes GraphicsPath.Widen(Pen pen) to produce strance results
				//pen.Alignment = PenAlignment.Inset;
				pen.LineJoin = lineStyle.LineJoin;
				pen.DashCap = lineStyle.DashCap;
				if (lineStyle.DashType == DashType.Solid)
					pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
				else {
					pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
					pen.DashPattern = lineStyle.DashPattern;
				}
				// create LineCaps
				if (startCapStyle != null) {
					if (startCapStyle.CapShape == CapShape.None) pen.StartCap = LineCap.Round;
					else {
						pen.StartCap = LineCap.Custom;
						pen.CustomStartCap = GetCustomLineCap(startCapStyle, lineStyle);
					}
				}
				if (endCapStyle != null) {
					if (endCapStyle.CapShape == CapShape.None) pen.EndCap = LineCap.Round;
					else {
						pen.EndCap = LineCap.Custom;
						pen.CustomEndCap = GetCustomLineCap(endCapStyle, lineStyle);
					}
				}

				// add created pen to the PenCache
				penCache.Add(penKey, pen);
			}
			return pen;
		}


		public static Brush GetBrush(IColorStyle colorStyle) {
			if (colorStyle == null) throw new ArgumentNullException("colorStyle");
			
			SolidBrush brush = null;
			solidBrushCache.TryGetValue(colorStyle, out brush);
			if (brush == null) {
				brush = new SolidBrush(GetColor(colorStyle, colorStyle.ConvertToGray));
				// add created brush to the BrushCache
				solidBrushCache.Add(colorStyle, brush);
			}
			return brush;
		}


		public static Brush GetBrush(IFillStyle fillStyle) {
			if (fillStyle == null) throw new ArgumentNullException("fillStyle");
			BrushKey brushKey;
			brushKey.FillStyle = fillStyle;
			brushKey.Image = null;

			Brush brush = null;
			brushCache.TryGetValue(brushKey, out brush);
			if (brush == null) {
				switch (fillStyle.FillMode) {
					case FillMode.Solid:
						brush = new SolidBrush(GetColor(fillStyle.BaseColorStyle, fillStyle.ConvertToGrayScale));
						break;
					case FillMode.Pattern:
						brush = new HatchBrush(fillStyle.FillPattern, GetColor(fillStyle.BaseColorStyle, fillStyle.ConvertToGrayScale), GetColor(fillStyle.AdditionalColorStyle, fillStyle.ConvertToGrayScale));
						break;
					case FillMode.Gradient:
						rectBuffer.X = 0;
						rectBuffer.Y = 0;
						rectBuffer.Width = 100;
						rectBuffer.Height = 100;
						brush = new LinearGradientBrush(rectBuffer, GetColor(fillStyle.AdditionalColorStyle, fillStyle.ConvertToGrayScale), GetColor(fillStyle.BaseColorStyle, fillStyle.ConvertToGrayScale), fillStyle.GradientAngle);
						break;
					case FillMode.Image:
						if (fillStyle.Image.Image == null)
							brush = new SolidBrush(Color.Transparent);
						else {
							// First, get ImageAttributes
							ImageAttributes imgAttribs = null;
							imageAttribsCache.TryGetValue(brushKey, out imgAttribs);
							if (imgAttribs == null) {
								imgAttribs = GdiHelpers.GetImageAttributes(fillStyle.ImageLayout, fillStyle.ImageGammaCorrection,
									fillStyle.ImageTransparency, fillStyle.ConvertToGrayScale);
								imageAttribsCache.Add(brushKey, imgAttribs);
							}

							// Create Brush
							rectBuffer.X = 0;
							rectBuffer.Y = 0;
							rectBuffer.Width = fillStyle.Image.Width;
							rectBuffer.Height = fillStyle.Image.Height;
							brush = new TextureBrush(fillStyle.Image.Image, rectBuffer, imgAttribs);
						}
						break;
					default: throw new nShapeUnsupportedValueException(fillStyle.FillMode);
				}

				// add created brush to the BrushCache
				if (brushCache.ContainsKey(brushKey))
					brushCache[brushKey] = brush;
				else brushCache.Add(brushKey, brush);
			}
			return brush;
		}


		public static TextureBrush GetBrush(Image image, nShapeImageLayout imageLayout, float gamma, byte transparency, bool grayScale) {
			if (image == null) throw new ArgumentNullException("image");
			BrushKey brushKey;
			brushKey.FillStyle = null;
			brushKey.Image = image;

			Brush brush = null;
			brushCache.TryGetValue(brushKey, out brush);
			if (!(brush is TextureBrush)) {
				// First, get ImageAttributes
				ImageAttributes imgAttribs = null;
				imageAttribsCache.TryGetValue(brushKey, out imgAttribs);
				if (imgAttribs == null) {
					imgAttribs = GdiHelpers.GetImageAttributes(imageLayout, gamma, transparency, grayScale);
					imageAttribsCache.Add(brushKey, imgAttribs);
				}

				// Create Brush
				rectBuffer.X = 0;
				rectBuffer.Y = 0;
				rectBuffer.Width = image.Width;
				rectBuffer.Height = image.Height;
				brush = new TextureBrush(image, rectBuffer, imgAttribs);

				// add created brush to the BrushCache
				if (brushCache.ContainsKey(brushKey))
					brushCache[brushKey] = brush;
				else brushCache.Add(brushKey, brush);
			}
			return (TextureBrush)brush;
		}


		public static Font GetFont(ICharacterStyle characterStyle) {
			if (characterStyle == null) throw new ArgumentNullException("characterStyle");

			Font font = null;
			fontCache.TryGetValue(characterStyle, out font);
			if (font == null) {
				FontFamily fontFamily = characterStyle.FontFamily;
				FontStyle style = characterStyle.Style;
				// check if the desired FontStyle is available for this particular FontFamily
				// Set an available FontStyle if not.
				if (fontFamily != null && !fontFamily.IsStyleAvailable(style)) {
					if (fontFamily.IsStyleAvailable(FontStyle.Regular)) {
						if (fontFamily.IsStyleAvailable(style | FontStyle.Regular))
							style |= FontStyle.Regular;
						else style = FontStyle.Regular;
					} else if (fontFamily.IsStyleAvailable(FontStyle.Bold)) {
						if (fontFamily.IsStyleAvailable(style | FontStyle.Bold))
							style |= FontStyle.Bold;
						else style = FontStyle.Bold;
					} else if (fontFamily.IsStyleAvailable(FontStyle.Italic)) {
						if (fontFamily.IsStyleAvailable(style | FontStyle.Italic))
							style |= FontStyle.Italic;
						else style = FontStyle.Italic;
					} else if (fontFamily.IsStyleAvailable(FontStyle.Strikeout)) {
						if (fontFamily.IsStyleAvailable(style | FontStyle.Strikeout))
							style |= FontStyle.Strikeout;
						else style = FontStyle.Strikeout;
					} else if (fontFamily.IsStyleAvailable(FontStyle.Underline)) {
						if (fontFamily.IsStyleAvailable(style | FontStyle.Underline))
							style |= FontStyle.Underline;
						else style = FontStyle.Underline;
					}
				}
				//font = new Font(fontFamily, characterStyle.SizeInPoints, style, GraphicsUnit.Point);
				font = new Font(fontFamily, characterStyle.Size, style, GraphicsUnit.Pixel);
				// add font to the FontCache
				fontCache.Add(characterStyle, font);
			}
			return font;
		}


		public static StringFormat GetStringFormat(IParagraphStyle paragraphStyle) {
			if (paragraphStyle == null) throw new ArgumentNullException("paragraphStyle");
			
			StringFormat stringFormat = null;
			stringFormatCache.TryGetValue(paragraphStyle, out stringFormat);
			if (stringFormat == null) {
				stringFormat = new StringFormat();
				switch (paragraphStyle.Alignment) {
					case ContentAlignment.BottomLeft:
						stringFormat.Alignment = StringAlignment.Near;
						stringFormat.LineAlignment = StringAlignment.Far;
						break;
					case ContentAlignment.BottomCenter:
						stringFormat.Alignment = StringAlignment.Center;
						stringFormat.LineAlignment = StringAlignment.Far;
						break;
					case ContentAlignment.BottomRight:
						stringFormat.Alignment = StringAlignment.Far;
						stringFormat.LineAlignment = StringAlignment.Far;
						break;
					case ContentAlignment.MiddleLeft:
						stringFormat.Alignment = StringAlignment.Near;
						stringFormat.LineAlignment = StringAlignment.Center;
						break;
					case ContentAlignment.MiddleCenter:
						stringFormat.Alignment = StringAlignment.Center;
						stringFormat.LineAlignment = StringAlignment.Center;
						break;
					case ContentAlignment.MiddleRight:
						stringFormat.Alignment = StringAlignment.Far;
						stringFormat.LineAlignment = StringAlignment.Center;
						break;
					case ContentAlignment.TopLeft:
						stringFormat.Alignment = StringAlignment.Near;
						stringFormat.LineAlignment = StringAlignment.Near;
						break;
					case ContentAlignment.TopCenter:
						stringFormat.Alignment = StringAlignment.Center;
						stringFormat.LineAlignment = StringAlignment.Near;
						break;
					case ContentAlignment.TopRight:
						stringFormat.Alignment = StringAlignment.Far;
						stringFormat.LineAlignment = StringAlignment.Near;
						break;
					default:
						throw new Exception(string.Format("Unexpected ContentAlignment value '{0}'.", paragraphStyle.Alignment));
				}
				// LineLimit prevents the Title from being drawn outside the layout rectangle.
				// If the layoutRectangle is too small, the text will not be rendered at all, so we do not use it.
				//stringFormat.FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.FitBlackBox | StringFormatFlags.LineLimit;
				stringFormat.FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.FitBlackBox;
				if (!paragraphStyle.WordWrap)
					stringFormat.FormatFlags |= StringFormatFlags.NoWrap;
				stringFormat.Trimming = paragraphStyle.Trimming;
				
				// add font to the FontCache
				stringFormatCache.Add(paragraphStyle, stringFormat);
			}
			return stringFormat;
		}

		#endregion


		#region [Private] Methods

		/// <summary>
		/// Deletes all tools based on the given CapStyle.
		/// </summary>
		private static void NotifyCapStyleChanged(ICapStyle capStyle) {
			Debug.Assert(capStyle != null);
			
			// collect affected PenKeys
			List<PenKey> penKeys = new List<PenKey>();
			foreach (KeyValuePair<PenKey, Pen> item in penCache)
				if (item.Key.StartCapStyle != null && item.Key.StartCapStyle == capStyle)
					penKeys.Add(item.Key);
				else if (item.Key.EndCapStyle != null && item.Key.EndCapStyle == capStyle)
					penKeys.Add(item.Key);
			// delete affected Pens
			foreach (PenKey penKey in penKeys) {
				Pen pen = penCache[penKey];
				penCache.Remove(penKey);
				pen.Dispose();
				pen = null;
			}
			penKeys.Clear();

			// collect affected CustomLineCaps
			List<CapKey> capKeys = new List<CapKey>();
			foreach (KeyValuePair<CapKey, CustomLineCap> item in capCache)
				if (item.Key.CapStyle == capStyle)
					capKeys.Add(item.Key);
			// delete affected CustomLineCaps
			foreach (CapKey capKey in capKeys) {
				CustomLineCap cap = capCache[capKey];
				capCache.Remove(capKey);
				cap.Dispose();
				cap = null;
			}

			// delete all GraphicsPaths
			foreach (CapKey capKey in capKeys) {
				if (capPathCache.ContainsKey(capKey)) {
					GraphicsPath path = capPathCache[capKey];
					capPathCache.Remove(capKey);
					path.Dispose();
					path = null;
				}
			}
			capKeys.Clear();
		}


		/// <summary>
		/// Deletes all tools based on the given CharacterStyle.
		/// </summary>
		private static void NotifyCharacterStyleChanged(ICharacterStyle characterStyle) {
			Debug.Assert(characterStyle != null);
			
			while (fontCache.ContainsKey(characterStyle)) {
				Font font = fontCache[characterStyle];
				fontCache.Remove(characterStyle);
				font.Dispose();
				font = null;
			}
		}


		/// <summary>
		/// Deletes all tools based on the given ColorStyle.
		/// </summary>
		private static void NotifyColorStyleChanged(IColorStyle colorStyle) {
			Debug.Assert(colorStyle != null);

			// dispose affected SolidBrushes
			while (solidBrushCache.ContainsKey(colorStyle)) {
				Brush brush = solidBrushCache[colorStyle];
				solidBrushCache.Remove(colorStyle);
				brush.Dispose();
				brush = null;
			}

			// collect affected Brushes
			List<IFillStyle> fillStyles = new List<IFillStyle>();
			foreach (KeyValuePair<BrushKey, Brush> item in brushCache) {
				if (item.Key.FillStyle != null
					&& item.Key.FillStyle.FillMode != FillMode.Image &&
					(item.Key.FillStyle.AdditionalColorStyle == colorStyle || item.Key.FillStyle.BaseColorStyle == colorStyle)) {
					fillStyles.Add(item.Key.FillStyle);
				}
			}
			// delete affected Brushes and notify that FillStyles have changed
			foreach (IFillStyle fillStyle in fillStyles)
				NotifyFillStyleChanged(fillStyle);
			fillStyles.Clear();


			// collect affected Pens
			List<PenKey> penKeys = new List<PenKey>();
			foreach (KeyValuePair<PenKey, Pen> item in penCache) {
				if (item.Key.LineStyle.ColorStyle == colorStyle)
					penKeys.Add(item.Key);
				else if (item.Key.StartCapStyle != null && item.Key.StartCapStyle.ColorStyle == colorStyle)
					penKeys.Add(item.Key);
				else if (item.Key.EndCapStyle != null && item.Key.EndCapStyle.ColorStyle == colorStyle)
					penKeys.Add(item.Key);
			}
			// delete affected Pens
			foreach (PenKey penKey in penKeys)
				// notify only affected LineStyles, affected CapStyles are notified later
				NotifyLineStyleChanged(penKey.LineStyle);
			penKeys.Clear();

			// collect affected CustomLineCaps
			List<CapKey> capKeys = new List<CapKey>();
			foreach (KeyValuePair<CapKey, CustomLineCap> item in capCache)
				if (item.Key.CapStyle.ColorStyle == colorStyle || item.Key.CapStyle.ColorStyle == colorStyle)
					capKeys.Add(item.Key);
			// delete affected CustomLineCaps
			foreach (CapKey capKey in capKeys)
				NotifyCapStyleChanged(capKey.CapStyle);
			capKeys.Clear();
		}


		/// <summary>
		/// Deletes all tools based on the given FillStyle.
		/// </summary>
		private static void NotifyFillStyleChanged(IFillStyle fillStyle) {
			Debug.Assert(fillStyle != null);
			BrushKey brushKey;
			brushKey.FillStyle = fillStyle;
			brushKey.Image = null;
			while (brushCache.ContainsKey(brushKey)) {
				Brush brush = brushCache[brushKey];
				brushCache.Remove(brushKey);
				brush.Dispose();
				brush = null;
			}
			while (imageAttribsCache.ContainsKey(brushKey)) {
				ImageAttributes imgAttribs = imageAttribsCache[brushKey];
				imageAttribsCache.Remove(brushKey);
				imgAttribs.Dispose();
				imgAttribs = null;
			}
		}


		/// <summary>
		/// Deletes all tools based on the given LineStyle.
		/// </summary>
		private static void NotifyLineStyleChanged(ILineStyle lineStyle) {
			Debug.Assert(lineStyle != null);

			// collect affected PenKeys
			List<PenKey> penKeys = new List<PenKey>();
			foreach (KeyValuePair<PenKey, Pen> item in penCache)
				if (item.Key.LineStyle == lineStyle)
					penKeys.Add(item.Key);
			// delete affected Pens
			foreach (PenKey penKey in penKeys) {
				Pen pen = penCache[penKey];
				penCache.Remove(penKey);
				pen.Dispose();
				pen = null;
			}
			penKeys.Clear();

			// collect affected CustomLineCaps
			List<CapKey> capKeys = new List<CapKey>();
			foreach (KeyValuePair<CapKey, CustomLineCap> item in capCache)
				if (item.Key.LineStyle == lineStyle)
					capKeys.Add(item.Key);
			// delete affected CustomLineCaps and their GraphicsPaths
			foreach (CapKey capKey in capKeys)
				NotifyCapStyleChanged(capKey.CapStyle);
			capKeys.Clear();
		}


		/// <summary>
		/// Deletes all tools based on the given ParagraphStyle.
		/// </summary>
		private static void NotifyParagraphStyleChanged(IParagraphStyle paragraphStyle) {
			Debug.Assert(paragraphStyle != null);
			while (stringFormatCache.ContainsKey(paragraphStyle)) {
				StringFormat stringFormat = stringFormatCache[paragraphStyle];
				stringFormatCache.Remove(paragraphStyle);
				stringFormat.Dispose();
				stringFormat = null;
			}
		}


		/// <summary>
		/// Deletes all tools based on the given ShapeStyle.
		/// </summary>
		private static void NotifyShapeStyleChanged(IShapeStyle shapeStyle) {
			// ShapeStyles not yet implemented
		}

	
		private static Color GetColor(IColorStyle colorStyle, bool convertToGray) {
			Debug.Assert(colorStyle != null);

			if (convertToGray) {
				int luminance = 0;
				luminance += (byte)Math.Round(colorStyle.Color.R * luminanceFactorRed);
				luminance += (byte)Math.Round(colorStyle.Color.G * luminanceFactorGreen);
				luminance += (byte)Math.Round(colorStyle.Color.B * luminanceFactorBlue);
				if (luminance > 255)
					luminance = 255;
				return Color.FromArgb(colorStyle.Color.A, luminance, luminance, luminance);
			} else return (colorStyle != null) ? colorStyle.Color : Color.Empty;
		}


		/// <summary>
		/// (re)calculates the given GraphicsPath according to the given CapShape.
		/// </summary>
		/// <param name="capPath">Reference of the GraphicsPath to (re)calculate</param>
		/// <param name="capShape">Desired shape of the LineCap</param>
		/// <param name="capSize">Desired Size of the LineCap</param>
		private static void CalcCapShape(ref GraphicsPath capPath, CapShape capShape, int capSize) {
			Debug.Assert(capPath != null);
			Debug.Assert(capSize >= 0);
			
			float halfSize = (float)capSize / 2;
			capPath.Reset();		
			switch (capShape) {
				case CapShape.ArrowClosed:
					capPath.StartFigure();
					capPath.AddLine(-halfSize, -capSize, 0, 0);
					capPath.AddLine(0, 0, halfSize, -capSize);
					capPath.AddLine(halfSize, -capSize, -halfSize, -capSize);
					capPath.CloseFigure();
					break;
				case CapShape.ArrowOpen:
					capPath.StartFigure();
					capPath.AddLine(0, 0 , -halfSize, -capSize);
					capPath.AddLine(-halfSize + 1, -capSize, 0, 1);
					capPath.AddLine(0, 1, halfSize + 1, -capSize);
					capPath.AddLine(halfSize, -capSize, 0, 0);
					capPath.CloseFigure();
					break;
				case CapShape.Triangle:
					capPath.StartFigure();
					capPath.AddLine(0, -capSize, -halfSize, 0);
					capPath.AddLine(-halfSize, 0, halfSize, 0);
					capPath.AddLine(halfSize, 0, 0, -capSize);
					capPath.CloseFigure();
					break;
				case CapShape.Circle:
					capPath.StartFigure();
					capPath.AddEllipse(-halfSize, -capSize, capSize, capSize);
					capPath.CloseFigure();
					break;
				case CapShape.Square:
					rectFBuffer.X = -halfSize;
					rectFBuffer.Y = -capSize;
					rectFBuffer.Width = halfSize + halfSize;
					rectFBuffer.Height = capSize;
					capPath.StartFigure();
					capPath.AddRectangle(rectFBuffer);
					capPath.CloseFigure();
					break;
				case CapShape.Diamond:
					capPath.StartFigure();
					capPath.AddLine(0, 0, -halfSize, -halfSize);
					capPath.AddLine(-halfSize, -halfSize, 0, -capSize);
					capPath.AddLine(0, -capSize, halfSize, -halfSize);
					capPath.AddLine(halfSize, -halfSize, 0, 0);
					capPath.CloseFigure();
					break;
				case CapShape.CenteredCircle:
					capPath.StartFigure();
					capPath.AddEllipse(-halfSize, -halfSize, capSize, capSize);
					capPath.CloseFigure();
					break;
				case CapShape.CenteredHalfCircle:
					capPath.StartFigure();
					capPath.StartFigure();
					capPath.AddArc(-halfSize, -halfSize, capSize, capSize, 0f, -180f);
					capPath.AddLine(-halfSize, 0, -halfSize - 1, 0);
					capPath.AddArc(-halfSize - 1, -halfSize - 1, capSize + 2, capSize + 2, 180f, 180f);
					capPath.AddLine(halfSize + 1, 0, halfSize, 0);
					capPath.CloseFigure();
					capPath.CloseFigure();
					break;
				case CapShape.None:
					return;
				default: throw new nShapeUnsupportedValueException(capShape);
			}
		}

		#endregion


		#region [Private] Types

		private struct PenKey {

			public static bool operator ==(PenKey a, PenKey b) {
				return (a.LineStyle == b.LineStyle
					&& a.StartCapStyle == b.StartCapStyle
					&& a.EndCapStyle == b.EndCapStyle);
			}

			public static bool operator !=(PenKey a, PenKey b) { return !(a == b); }

			public ILineStyle LineStyle;
			
			public ICapStyle StartCapStyle;
			
			public ICapStyle EndCapStyle;

			public override bool Equals(object obj) { return (obj is PenKey && this == (PenKey)obj); }
			
			public override int GetHashCode() {
				int hashCode = 0;
				if (LineStyle != null) hashCode ^= LineStyle.GetHashCode();
				if (StartCapStyle != null) hashCode ^= StartCapStyle.GetHashCode();
				if (EndCapStyle != null) hashCode ^= EndCapStyle.GetHashCode();
				return hashCode;
			}
		
		}


		private struct CapKey {

			public static bool operator ==(CapKey a, CapKey b) { 
				return (a.LineStyle == b.LineStyle && a.CapStyle == b.CapStyle); 
			}

			public static bool operator !=(CapKey a, CapKey b) { return !(a == b); }

			public ICapStyle CapStyle;

			public ILineStyle LineStyle;

			public override bool Equals(object obj) { return (obj is CapKey && this == (CapKey)obj); }

			public override int GetHashCode() {
				int hashCode = 0;
				if (LineStyle != null) hashCode ^= LineStyle.GetHashCode();
				if (CapStyle != null) hashCode ^= CapStyle.GetHashCode();
				return hashCode;
			}

		}


		private struct BrushKey {

			public static bool operator ==(BrushKey a, BrushKey b) {
				return (a.FillStyle == b.FillStyle
					&& a.Image == b.Image);
			}

			public static bool operator !=(BrushKey a, BrushKey b) { return !(a == b); }

			public IFillStyle FillStyle;
			
			public Image Image;
			
			public override bool Equals(object obj) { return (obj is BrushKey && this == (BrushKey)obj); }
			
			public override int GetHashCode() {
				int hashCode = 0;
				if (FillStyle != null) hashCode ^= FillStyle.GetHashCode();
				if (Image != null) hashCode ^= Image.GetHashCode();
				return hashCode;
			}
		
		}

		#endregion


		#region  Fields

		private static Dictionary<PenKey, Pen> penCache = new Dictionary<PenKey, Pen>(50);
		private static Dictionary<IColorStyle, SolidBrush> solidBrushCache = new Dictionary<IColorStyle, SolidBrush>(50);
		private static Dictionary<BrushKey, Brush> brushCache = new Dictionary<BrushKey, Brush>(20);
		private static Dictionary<BrushKey, ImageAttributes> imageAttribsCache = new Dictionary<BrushKey, ImageAttributes>(5);
		private static Dictionary<ICharacterStyle, Font> fontCache = new Dictionary<ICharacterStyle, Font>(10);
		private static Dictionary<IParagraphStyle, StringFormat> stringFormatCache = new Dictionary<IParagraphStyle, StringFormat>(5);
		private static Dictionary<CapKey, GraphicsPath> capPathCache = new Dictionary<CapKey, GraphicsPath>(10);
		private static Dictionary<CapKey, CustomLineCap> capCache = new Dictionary<CapKey, CustomLineCap>(10);
		
		private static Rectangle rectBuffer = Rectangle.Empty;		// Rectangle buffer 
		private static RectangleF rectFBuffer = RectangleF.Empty;	// RectangleF buffer
		private static PointF[] pointFBuffer = new PointF[0];
		private static Matrix matrix = new Matrix();						// Matrix for transformations

		// constants for the color-to-greyscale conversion
		// luminance correction factor (the human eye has preferences regarding colors)
		private const float luminanceFactorRed = 0.3f;
		private const float luminanceFactorGreen = 0.59f;
		private const float luminanceFactorBlue = 0.11f;
		// Alternative values
		//private const float luminanceFactorRed = 0.3f;
		//private const float luminanceFactorGreen = 0.5f;
		//private const float luminanceFactorBlue = 0.3f;
		
		#endregion
	}

}
