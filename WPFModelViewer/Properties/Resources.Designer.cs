﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace WPFModelViewer.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("WPFModelViewer.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] _default {
            get {
                object obj = ResourceManager.GetObject("_default", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to void main()
        ///{	
        ///	gl_FragColor = vec4(0.8, 0.0, 0.0, 1.0);	
        ///}.
        /// </summary>
        internal static string camera_frag {
            get {
                return ResourceManager.GetString("camera_frag", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to /* Copies incoming vertex color without change.
        /// * Applies the transformation matrix to vertex position.
        /// */
        ///layout(location=0) in vec4 vPosition;
        ///layout(location=1) in vec4 nPosition; //normals
        ///uniform mat4 self_mvp, mvp;
        ///
        /////Outputs
        ///void main()
        ///{
        ///	vec4 inv_pos = inverse(self_mvp) * vPosition;
        ///
        ///	inv_pos.z = min(inv_pos.z, 1000);
        ///
        ///    gl_Position = mvp * inv_pos;
        ///
        ///	//gl_Position = mvp * vPosition;
        ///	gl_Position = gl_Position * 1.0f/gl_Position.w;
        ///}.
        /// </summary>
        internal static string camera_vert {
            get {
                return ResourceManager.GetString("camera_vert", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] default_mask {
            get {
                object obj = ResourceManager.GetObject("default_mask", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] droid_fnt {
            get {
                object obj = ResourceManager.GetObject("droid_fnt", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap droid_png {
            get {
                object obj = ResourceManager.GetObject("droid_png", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap ianm32logo_border {
            get {
                object obj = ResourceManager.GetObject("ianm32logo_border", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] segoe_fnt {
            get {
                object obj = ResourceManager.GetObject("segoe_fnt", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap segoe_png {
            get {
                object obj = ResourceManager.GetObject("segoe_png", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
    }
}
