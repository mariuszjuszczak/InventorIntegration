using System;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Inventor;


namespace InventorIntegration
{
    public class InventorCamera
    {
        private Inventor.Application _invApp = null;
        private TransientGeometry oTG = null;
        private Camera oCamera = null;
        private Inventor.View oView = null;
        private bool _started = false;
        private bool _opened = false;
        private string docType;
        private double DEG_TO_RAD = Math.PI / 180;
        public InventorCamera()
        {
            if (_invApp == null)
            {
                try
                {
                    _invApp = (Inventor.Application)Marshal.GetActiveObject("Inventor.Application");
                    _started = true;
                }
                catch (Exception ex)
                {
                    _started = false;
                    _invApp = null;
                    return;
                }
            }
            if (_invApp.Documents.Count == 0)
            {
                _opened = false;
                return;
            }

            if (_invApp.ActiveDocument.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject || _invApp.ActiveDocument.DocumentType == DocumentTypeEnum.kPartDocumentObject || _invApp.ActiveDocument.DocumentType == DocumentTypeEnum.kPresentationDocumentObject)
            {
                FindType();
                _opened = true;
                oView = _invApp.ActiveView;
                oTG = _invApp.TransientGeometry;
                ReturnHome();
                oCamera = oView.Camera;
            }
        }

        private void FindType()
        {
            switch (_invApp.ActiveDocument.DocumentType)
            {
                case DocumentTypeEnum.kAssemblyDocumentObject:
                    docType = "ASSEMBLY";
                    break;
                case DocumentTypeEnum.kPartDocumentObject:
                    docType = "PART";
                    break;
                case DocumentTypeEnum.kPresentationDocumentObject:
                    docType = "PRESENTATION";
                    break;
            }
        }

        public string GetDocType()
        {
            return docType;
        }

        public bool IsOpened()
        {
            return _opened;
        }

        public bool IsStarted()
        {
            return _started;
        }

        public void Zoom(double scale)
        {
            double height, width;
            oCamera.GetExtents(out width, out height);
            oCamera.SetExtents(width + (width * scale), height + (height * scale));
            oCamera.ApplyWithoutTransition();
        }

        public void GetCurrentCamera()
        {
            if (!_started)
            {
                try
                {
                    _invApp = (Inventor.Application)Marshal.GetActiveObject("Inventor.Application");
                    _started = true;
                }
                catch (Exception ex)

                {
                    _started = false;
                    _invApp = null;
                    return;
                }
            }
            else
            {
                try
                {
                    if (_invApp.Documents.Count == 0)
                    {
                        _opened = false;
                        return;
                    }
                    if (!_opened)
                    {
                        if (_invApp.ActiveDocument.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject || _invApp.ActiveDocument.DocumentType == DocumentTypeEnum.kPartDocumentObject || _invApp.ActiveDocument.DocumentType == DocumentTypeEnum.kPresentationDocumentObject)
                        {
                            FindType();
                            oTG = _invApp.TransientGeometry;
                            oView = _invApp.ActiveView;
                            ReturnHome();
                            oCamera = oView.Camera;
                            _opened = true;                       
                        }
                    }
                    else
                    {
                        oView = _invApp.ActiveView;
                        oCamera = oView.Camera;
                    }
                }
                catch (Exception ex)
                {
                    _started = false;
                    _opened = false;
                }
            }
        }
        public void ChangeView(double gyroY, double gyroX, double gyroZ, bool apply)
        {
            Inventor.Vector screenX = null;
            Inventor.Vector screenY = null;
            Inventor.Vector screenZ = null;
            screenZ = oCamera.Target.VectorTo(oCamera.Eye);
            screenZ.Normalize();

            screenY = oCamera.UpVector.AsVector();
            screenX = screenY.CrossProduct(screenZ);

            Inventor.Matrix rotX = oTG.CreateMatrix();
            rotX.SetToRotation(gyroX, screenX, oCamera.Target);

            Inventor.Matrix rotY = oTG.CreateMatrix();
            rotY.SetToRotation(gyroY, screenY, oCamera.Target);

            Inventor.Matrix rotZ = oTG.CreateMatrix();
            rotZ.SetToRotation(gyroZ, screenZ, oCamera.Target);

            Inventor.Matrix rot = oTG.CreateMatrix();
            rot = rotX;
            rot.PostMultiplyBy(rotY);
            rot.PostMultiplyBy(rotZ);
            Inventor.Point newEye = oCamera.Eye;
            newEye.TransformBy(rot);
            Inventor.UnitVector newUp = oCamera.UpVector;
            newUp.TransformBy(rot);
            oCamera.Eye = newEye;
            oCamera.UpVector = newUp;
            if (apply)
                oCamera.Apply();
            else
                oCamera.ApplyWithoutTransition();
        }

        public void TranslateView(double scaleX, double scaleY)
        {
            Inventor.Vector X = null;
            Inventor.Vector Y = null;
            Inventor.Vector Z = null;
            Z = oCamera.Target.VectorTo(oCamera.Eye);
            Z.Normalize();

            Y = oCamera.UpVector.AsVector();
            X = Y.CrossProduct(Z);

            X.ScaleBy(scaleX);
            Y.ScaleBy(scaleY);

            Inventor.Point newEye = oCamera.Eye;
            newEye.TranslateBy(X);
            newEye.TranslateBy(Y);
            Inventor.Point newTarget = oCamera.Target;
            newTarget.TranslateBy(X);
            newTarget.TranslateBy(Y);
            oCamera.Eye = newEye;
            oCamera.Target = newTarget;
            oCamera.ApplyWithoutTransition();
        }

        public void ReturnHome()
        {
            oView.GoHome();
        }

        public void RotateCube(string direction)
        {
            switch (direction)
            {
                case "LEFT":
                    ChangeView(90 * DEG_TO_RAD, 0, 0, true);
                    break;
                case "RIGHT":
                    ChangeView(-90 * DEG_TO_RAD, 0, 0, true);
                    break;
                case "UP":
                    ChangeView(0, 90 * DEG_TO_RAD, 0, true);
                    break;
                case "DOWN":
                    ChangeView(0, -90 * DEG_TO_RAD, 0, true);
                    break;
            }
        }
    }
}