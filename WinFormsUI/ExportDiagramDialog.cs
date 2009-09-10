﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Dataweb.nShape.Controllers;
using Dataweb.nShape.Advanced;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace Dataweb.nShape.WinFormsUI {

	public partial class ExportDiagramDialog : Form {
		
		public ExportDiagramDialog(IDiagramPresenter diagramPresenter) {
			InitializeComponent();

			if (diagramPresenter == null) throw new ArgumentNullException("diagramPresenter");
			this.diagramPresenter = diagramPresenter;
			InitializeDialog();
		}


		private void InitializeDialog() {
			dpiComboBox.Items.Clear();
			using(Graphics gfx = Graphics.FromHwnd(IntPtr.Zero)) {
				dpiComboBox.Items.Add((int)gfx.DpiY);
				dpiComboBox.Items.Add(150);
				dpiComboBox.Items.Add(300);
				dpiComboBox.Items.Add(600);
			}
			dpiComboBox.SelectedIndex = 0;

			colorLabel.BackColor = Color.White;

			backColorCheckBox.Checked = false;
			marginUpDown.Value = 0;
			emfPlusRadioButton.Checked = true;
			toFileRadioButton.Checked = true;
			exportAllRadioButton.Checked = true;

			//exportSelectedRadioButton.Checked =
			exportSelectedRadioButton.Enabled = (diagramPresenter.SelectedShapes.Count > 0);

			EnableOkButton();
		}


		~ExportDiagramDialog() {
			if (imgAttribs != null) imgAttribs.Dispose();
			imgAttribs = null;
			if (colorLabelBackBrush != null) colorLabelBackBrush.Dispose();
			colorLabelBackBrush = null;
			if (colorLabelFrontBrush != null) colorLabelFrontBrush.Dispose();
			colorLabelFrontBrush = null;
		}


		#region "File Format Options" event handler implementations

		private void emfPlusRadioButton_CheckedChanged(object sender, EventArgs e) {
			if (emfPlusRadioButton.Checked) {
				imageFormat = nShapeImageFormat.EmfPlus;
				descriptionLabel.Text = emfPlusDescription;
				EnableResolutionAndQualitySelection();
				RefreshPreview();
			}
		}


		private void emfRadioButton_CheckedChanged(object sender, EventArgs e) {
			if (emfRadioButton.Checked) {
				imageFormat = nShapeImageFormat.Emf;
				descriptionLabel.Text = emfDescription;
				EnableResolutionAndQualitySelection();
				RefreshPreview();
			}
		}


		private void pngRadioButton_CheckedChanged(object sender, EventArgs e) {
			if (pngRadioButton.Checked) {
				imageFormat = nShapeImageFormat.Png;
				descriptionLabel.Text = pngDescription;
				EnableResolutionAndQualitySelection();
				RefreshPreview();
			}
		}


		private void jpgRadioButton_CheckedChanged(object sender, EventArgs e) {
			if (jpgRadioButton.Checked) {
				imageFormat = nShapeImageFormat.Jpeg;
				descriptionLabel.Text = jpgDescription;
				EnableResolutionAndQualitySelection();
				backColorCheckBox.Checked = true;
				RefreshPreview();
			}
		}


		private void bmpRadioButton_CheckedChanged(object sender, EventArgs e) {
			if (bmpRadioButton.Checked) {
				imageFormat = nShapeImageFormat.Bmp;
				descriptionLabel.Text = bmpDescription;
				EnableResolutionAndQualitySelection();
				backColorCheckBox.Checked = true;
				RefreshPreview();
			}
		}

		#endregion


		#region "Export Options" event handler implementations
		
		private void toClipboardRadioButton_CheckedChanged(object sender, EventArgs e) {
			exportToClipboard = true;
			EnableFileSelection();
			EnableOkButton();
		}


		private void toFileRadioButton_CheckedChanged(object sender, EventArgs e) {
			exportToClipboard = false;
			EnableFileSelection();
			EnableOkButton();
		}


		private void filePathTextBox_TextChanged(object sender, EventArgs e) {
			if (filePathTextBox.Text != filePath)
				SetFilePath(filePathTextBox.Text);
		}

	
		private void browseButton_Click(object sender, EventArgs e) {
			string fileFilter = null;
			switch (imageFormat) {
				case nShapeImageFormat.Bmp: fileFilter = "Bitmap Picture Files|*.bmp|All Files|*.*"; break;
				case nShapeImageFormat.EmfPlus: 
				case nShapeImageFormat.Emf: fileFilter = "Enhanced Meta Files|*.emf|All Files|*.*"; break;
				case nShapeImageFormat.Gif: fileFilter = "Graphics Interchange Format Files|*.gif|All Files|*.*"; break;
				case nShapeImageFormat.Jpeg: fileFilter = "Joint Photographic Experts Group (JPEG) Files|*.jpeg;*.jpg|All Files|*.*"; break;
				case nShapeImageFormat.Png: fileFilter = "Portable Network Graphics Files|*.png|All Files|*.*"; break;
				case nShapeImageFormat.Tiff: fileFilter = "Tagged Image File Format Files|*.tiff;*.tif|All Files|*.*"; break;
				default: throw new nShapeUnsupportedValueException(imageFormat);
			}
			saveFileDialog.Filter = fileFilter;
			if (saveFileDialog.ShowDialog() == DialogResult.OK)
				SetFilePath(saveFileDialog.FileName);
			else SetFilePath(string.Empty);
		}


		private void dpiComboBox_SelectedValueChanged(object sender, EventArgs e) {
		}


		private void dpiComboBox_SelectedIndexChanged(object sender, EventArgs e) {

		}

		
		private void dpiComboBox_TextChanged(object sender, EventArgs e) {
			//if (!string.IsNullOrEmpty(dpiComboBox.Text)) {
				int value;
				if (!int.TryParse(dpiComboBox.Text, out value)) {
					if (!string.IsNullOrEmpty(dpiComboBox.Text))
						dpiComboBox.Text = string.Empty;
					value = -1;
				}
				if (value != dpi) {
					dpi = value;
					RefreshPreview();
				}
			//}
		}


		private void qualityTrackBar_ValueChanged(object sender, EventArgs e) {
			compressionQuality = (byte)qualityTrackBar.Value;
		}
		
		#endregion


		#region "Content Options" event handler implementations

		private void exportSelectedRadioButton_CheckedChanged(object sender, EventArgs e) {
			shapes = diagramPresenter.SelectedShapes;
			RefreshPreview();
		}


		private void exportAllRadioButton_CheckedChanged(object sender, EventArgs e) {
			shapes = diagramPresenter.Diagram.Shapes;
			RefreshPreview();
		}


		private void exportDiagramRadioButton_CheckedChanged(object sender, EventArgs e) {
			shapes = null;
			RefreshPreview();
		}


		private void withBackgroundCheckBox_CheckedChanged(object sender, EventArgs e) {
			RefreshPreview();
		}


		private void marginUpDown_ValueChanged(object sender, EventArgs e) {
			margin = (int)marginUpDown.Value;
			RefreshPreview();
		}


		private void backColorCheckBox_CheckedChanged(object sender, EventArgs e) {
			if (backColorCheckBox.Checked) 
				SetBackgroundColor(colorLabel.BackColor);
			else SetBackgroundColor(Color.Transparent);
		}


		private void selectBackColor_Click(object sender, EventArgs e) {
			colorDialog.Color = backgroundColor;
			colorDialog.SolidColorOnly = false;
			colorDialog.AllowFullOpen = true;
			colorDialog.AnyColor = true;
			if (colorDialog.ShowDialog(this) == DialogResult.OK) {
				colorLabel.BackColor = colorDialog.Color;
				if (backColorCheckBox.Checked) SetBackgroundColor(colorLabel.BackColor);
				else backColorCheckBox.Checked = true;
			}
		}
		
		#endregion


		#region Event handler implementations

		private void previewCheckBox_CheckedChanged(object sender, EventArgs e) {
			RefreshPreview();
		}

	
		private void previewPanel_Paint(object sender, PaintEventArgs e) {
			if (previewCheckBox.Checked) {
				// Apply graphics settings
				GdiHelpers.ApplyGraphicsSettings(e.Graphics, nShapeRenderingQuality.MaximumQuality);
				// Create image
				if (image == null) CreateImage();
				if (imgAttribs == null) imgAttribs = GdiHelpers.GetImageAttributes(imageLayout);
				// Draw image
				Rectangle bounds = previewPanel.ClientRectangle;
				GdiHelpers.DrawImage(e.Graphics, image, imgAttribs, imageLayout, bounds, bounds);
			}
		}


		private void backColorLabel_Paint(object sender, PaintEventArgs e) {
			// Draw a pattern in order to make transparency of colors visible
			if (colorLabelBackBrush == null)
				colorLabelBackBrush = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.White, Color.Black);
			if (colorLabelFrontBrush == null) colorLabelFrontBrush = new SolidBrush(backgroundColor);
			e.Graphics.FillRectangle(colorLabelBackBrush, e.ClipRectangle);
			e.Graphics.FillRectangle(colorLabelFrontBrush, e.ClipRectangle);
		}

	
		private void exportButton_Click(object sender, EventArgs e) {
			ExportImage();
		}


		private void okButton_Click(object sender, EventArgs e) {
			ExportImage();
			if (Modal) DialogResult = DialogResult.OK;
			else Close();
			DeleteImage();
		}


		private void cancelButton_Click(object sender, EventArgs e) {
			if (this.Modal) DialogResult = DialogResult.Cancel;
			else Close();
			DeleteImage();
		}

		#endregion

	
		private void RefreshPreview() {
			DeleteImage();
			previewPanel.Invalidate();
		}
		
		
		private void CreateImage() {
			if (image != null) image.Dispose();
			image = diagramPresenter.Diagram.CreateImage(imageFormat,
				shapes,
				margin,
				withBackgroundCheckBox.Checked,
				backgroundColor,
				dpi);
			GraphicsUnit unit = GraphicsUnit.Display;
			imageBounds = Rectangle.Round(image.GetBounds(ref unit));
		}


		private void ExportImage() {
			if (image == null) CreateImage();
			switch (imageFormat) {
				case nShapeImageFormat.Emf:
				case nShapeImageFormat.EmfPlus:
					if (exportToClipboard)
						EmfHelper.PutEnhMetafileOnClipboard(this.Handle, (Metafile)image.Clone());
					else GdiHelpers.SaveImageToFile(image, filePath, imageFormat, compressionQuality);
					break;
				case nShapeImageFormat.Bmp:
				case nShapeImageFormat.Gif:
				case nShapeImageFormat.Jpeg:
				case nShapeImageFormat.Png:
				case nShapeImageFormat.Tiff:
					if (exportToClipboard)
						Clipboard.SetImage((Image)image.Clone());
					else GdiHelpers.SaveImageToFile(image, filePath, imageFormat, compressionQuality);
					break;
				case nShapeImageFormat.Svg:
					throw new NotImplementedException();
				default: throw new nShapeUnsupportedValueException(imageFormat);
			}
		}
		
		
		private void DeleteImage() {
			if (image != null) image.Dispose();
			image = null;
		}


		private void SetBackgroundColor(Color color) {
			backgroundColor = color;
			if (colorLabelFrontBrush != null) colorLabelFrontBrush.Dispose();
			colorLabelFrontBrush = null;
			colorLabel.Invalidate();
			RefreshPreview();
		}


		private void SetFilePath(string path) {
			filePath = path;
			if (filePathTextBox.Text != filePath) 
				filePathTextBox.Text = filePath;
			EnableOkButton();
		}


		private void EnableOkButton() {
			exportButton.Enabled = 
			okButton.Enabled = (exportToClipboard || !string.IsNullOrEmpty(filePath));
		}
		
		
		private void EnableFileSelection() {
			filePathTextBox.Enabled =
			browseButton.Enabled = !exportToClipboard;
			EnableResolutionAndQualitySelection();
		}


		private void EnableResolutionAndQualitySelection() {
			bool enable;
			switch (imageFormat) {
				case nShapeImageFormat.EmfPlus:
				case nShapeImageFormat.Emf:
				case nShapeImageFormat.Svg:
					enable = false; break;
				case nShapeImageFormat.Bmp:
				case nShapeImageFormat.Gif:
				case nShapeImageFormat.Jpeg:
				case nShapeImageFormat.Png:
				case nShapeImageFormat.Tiff:
					enable = !exportToClipboard; break;
				default: enable = false; break;
			}
			dpiLabel.Enabled =
			dpiComboBox.Enabled =
			qualityLabel.Enabled =
			qualityTrackBar.Enabled = enable;
		}


		#region Fields

		private const string emfPlusDescription = "Windows Enhanced Metafile Plus (*.emf)\nCreates a high quality vector image file supporting transparency and translucency. The Emf Plus file format is backwards compatible with the classic Emf format.";
		private const string emfDescription = "Windows Enhanced Metafile (*.emf)\nCreates a low quality vector image file supporting transparency and (emulated) translucency.";
		private const string pngDescription = "Portable Network graphics (*.png)\nCreates a bitmap image file supporting transparency. The Png file format provides medium but lossless compression.";
		private const string jpgDescription = "Joint Photographic Experts Group (*.jpeg)\nCreates a compressed bitmap image file. The Jpg file format does not support transparency. It provides an adjustable (lossy) compression.";
		private const string bmpDescription = "Bitmap (*.png)\nCreates an uncompressed bitmap image file. The Bmp file format does not support transparency.";

		// Rendering stuff
		private const nShapeImageLayout imageLayout = nShapeImageLayout.Fit;
		private Image image = null;
		private Rectangle imageBounds = Rectangle.Empty;

		// Image content stuff
		private int margin;
		private int dpi;
		private Color backgroundColor = Color.Transparent;
		private IEnumerable<Shape> shapes;

		// Export stuff
		private nShapeImageFormat imageFormat;
		private bool exportToClipboard;
		private byte compressionQuality = 75;
		private string filePath = null;

		// Fields
		private IDiagramPresenter diagramPresenter = null;
		private ImageAttributes imgAttribs = null;
		private Brush colorLabelBackBrush = null;
		private Brush colorLabelFrontBrush = null;

		#endregion
	}
}
