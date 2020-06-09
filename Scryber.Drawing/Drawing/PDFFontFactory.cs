﻿/*  Copyright 2012 PerceiveIT Limited
 *  This file is part of the Scryber library.
 *
 *  You can redistribute Scryber and/or modify 
 *  it under the terms of the GNU Lesser General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 * 
 *  Scryber is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Lesser General Public License for more details.
 * 
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with Scryber source code in the COPYING.txt file.  If not, see <http://www.gnu.org/licenses/>.
 * 
 */

#define FALLBACKTOREGULAR //comment out if we should not fall back to regular font style if the bold / italic variants are not found.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Text;
using Scryber.Resources;
using Scryber.Configuration;

namespace Scryber.Drawing
{
    //TODO: change the enumeration names to PDF...

     
    public static class PDFFontFactory
    {
        //
        // Inner classes
        //

        #region private abstract class FontReference
        /// <summary>
        /// A unique font reference
        /// </summary>
        private abstract class FontReference
        {
            internal string FamilyName { get; private set; }
            internal System.Drawing.FontStyle Style { get; private set; }
            internal string FilePath { get; private set; }
            internal byte[] FileData { get; private set; }
            internal int FileHeadOffset { get; private set; }

            internal PDFFontDefinition Definition { get; set; }

            internal bool LoadedDefintion { get { return null != Definition; } }

            internal FontFamily SystemFamily { get; set; }

            internal object LoadLock { get; private set; }
            
            internal FontReference(FontFamily family, string name, System.Drawing.FontStyle style, string path, int fileHeadOffset)
            {
                this.SystemFamily = family;
                this.FamilyName = name;
                this.Style = style;
                this.FilePath = path;
                this.FileHeadOffset = fileHeadOffset;
                this.LoadLock = new object();
            }

            internal FontReference(FontFamily family, string name, System.Drawing.FontStyle style, byte[] data, int fileHeadOffset)
            {
                this.SystemFamily = family;
                this.FamilyName = name;
                this.Style = style;
                this.FilePath = null;
                this.LoadLock = new object();
                this.FileHeadOffset = fileHeadOffset;
                this.FileData = data;
            }
        }

        #endregion

        #region private class LinkedFontReference : FontReference

        /// <summary>
        /// A unique font reference that can be chained
        /// </summary>
        private class LinkedFontReference : FontReference
        {
            internal LinkedFontReference Next { get; set; }

            internal LinkedFontReference(FontFamily family, string familyname, System.Drawing.FontStyle style, string path, int fileHeadOffset)
                : base(family, familyname, style, path, fileHeadOffset)
            {
            }

            internal LinkedFontReference(FontFamily family, string familyname, System.Drawing.FontStyle style, byte[] data, int fileHeadOffset)
                : base(family, familyname, style, data, fileHeadOffset)
            {
            }

            internal void Append(LinkedFontReference reference)
            {
                if (null == this.Next)
                    this.Next = reference;
                else
                    this.Next.Append(reference);
            }

            internal LinkedFontReference GetStyle(System.Drawing.FontStyle style)
            {
                if (style == this.Style)
                    return this;
                else if (null == this.Next)
                    return null;
                else
                    return this.Next.GetStyle(style);
            }
        }

        #endregion

        #region private class FamilyReference

        /// <summary>
        /// A family of Font References
        /// </summary>
        private class FamilyReference
        {
            private FontFamily _sysfam;

            internal string FamilyName { get; private set; }
            internal FontFamily SystemFamily {
                get { return _sysfam; }
                set
                {
                    _sysfam = value;
                    LinkedFontReference link = _first;
                    while (null != link)
                    {
                        link.SystemFamily = value;
                        link = link.Next;
                    }
                }
            }

            private LinkedFontReference _first;

            internal FontReference First
            {
                get { return _first; }
            }

            internal FontReference this[System.Drawing.FontStyle style]
            {
                get
                {
                    if (null == _first)
                        return null;
                    else
                        return _first.GetStyle(style);
                }
            }

            internal FamilyReference(string name)
            {
                this.FamilyName = name;
            }

            internal void Add(System.Drawing.FontStyle style, string filepath, int fileHeadOffset)
            {

                LinkedFontReference fontref = new LinkedFontReference(this.SystemFamily, this.FamilyName, style, filepath, fileHeadOffset);
                if (null == _first)
                    _first = fontref;
                else
                    _first.Append(fontref);
            }

            internal void Add(System.Drawing.FontStyle style, byte[] data, int fileHeadOffset)
            {
                LinkedFontReference fontref = new LinkedFontReference(this.SystemFamily, this.FamilyName, style, data, fileHeadOffset);
                if (null == _first)
                    _first = fontref;
                else
                    _first.Append(fontref);
            }

            internal bool TryGetFont(System.Drawing.FontStyle style, out FontReference font)
            {
                var found = _first;
                while(null != found)
                {
                    if(found.Style == style)
                    {
                        font = found;
                        return true;
                    }
                }

                font = null;
                return false;
            }
        }

        #endregion

        #region private class FamilyReferenceBag

        /// <summary>
        /// A collection of Font Family references that can be acessed by family name and font style. 
        /// Does not throw an exception if the font does not exist.
        /// </summary>
        private class FamilyReferenceBag
        {
            private Dictionary<string, FamilyReference> _families;
            private FontCollection _collection;

            //internal FontCollection FontCollection
            //{
            //    get { return _collection; }
            //}

            internal FamilyReferenceBag(FontCollection collection)
            {
                _collection = collection;
                _families = new Dictionary<string, FamilyReference>(StringComparer.OrdinalIgnoreCase);
            }

            internal FamilyReference this[string family]
            {
                get
                {
                    FamilyReference fm;
                    if (_families.TryGetValue(family, out fm))
                        return fm;
                    else
                        return null;
                }
            }

            internal FontReference this[string family, System.Drawing.FontStyle style]
            {
                get
                {
                    FontReference fnt;
                    FamilyReference fam;
                    if (_families.TryGetValue(family, out fam))
                    {
                        fnt = fam[style];
                    }
                    else
                        fnt = null;

                    return fnt;
                }
            }

            internal void FillAllFamilies(List<string> families)
            {
                foreach (string name in this._families.Keys)
                {
                    families.Add(name);
                }
            }

            internal virtual void AddFontFile(string family, System.Drawing.FontStyle style, string path, int fileHeadOffset)
            {
                FamilyReference fam;
                if (_families.TryGetValue(family, out fam) == false)
                {
                    fam = new FamilyReference(family);
                    _families.Add(family, fam);
                }
                fam.Add(style, path, fileHeadOffset);
            }

            internal void AddFontResource(string family, System.Drawing.FontStyle style, byte[] data, int fileHeadOffset)
            {
                FamilyReference fam;
                if (_families.TryGetValue(family, out fam) == false)
                {
                    fam = new FamilyReference(family);
                    _families.Add(family, fam);
                }
                fam.Add(style, data, fileHeadOffset);
            }

            internal void AddFontFamily(string name, FamilyReference reference)
            {
                _families.Add(name, reference);
            }

            internal bool TryGetFamily(string name, out FamilyReference reference)
            {
                return _families.TryGetValue(name, out reference);
            }

            internal bool TryGetFont(string name, System.Drawing.FontStyle style, out FontReference reference)
            {
                reference = null;
                FamilyReference family;
                if (_families.TryGetValue(name, out family) && family.TryGetFont(style, out reference))
                    return true;
                else
                    return false;
            }
        }

        #endregion


        //
        //Static variables
        //

        
        private static FamilyReferenceBag _systemfamilies;
        private static FamilyReferenceBag _customfamilies;
        private static FamilyReferenceBag _genericfamilies;
        private static bool _init;
        private static Exception _initex;
        private static object _initlock;

        //
        // ..ctor
        //

        #region static PDFFontFactory()

        static PDFFontFactory()
        {
            _systemfamilies = null;
            _customfamilies = null;
            _init = false;
            _initlock = new object();
            _initex = null;
        }

        #endregion

        //
        // public methods
        //

        #region  internal static string GetSystemFontFamilyNameForStandardFont(string pdffamilyname)

        /// <summary>
        /// Gets the system name of the font for the PDF family name
        /// </summary>
        /// <param name="pdffamilyname">The name of the PDF font family</param>
        /// <returns></returns>
        public static string GetSystemFontFamilyNameForStandardFont(string pdffamilyname)
        {
            if (string.IsNullOrEmpty(pdffamilyname))
                throw new ArgumentNullException("pdfFamilyName");

            string name = pdffamilyname.Replace(" ", "");
            string newName;
            switch (name.ToLower())
            {
                case "courier":
                    newName = "Courier New";
                    break;
                case "times":
                    newName = "Times New Roman";
                    break;
                case "arial":
                case "helvetica":
                    newName = "Arial";
                    break;
                case "symbol":
                    newName = "Symbol Regular";
                    break;
                case "zapfdingbats":
                    newName = "Zapf Dingbats";
                    break;
                default:
                    throw new ArgumentException(String.Format(Errors.FontNotFound, name),"font");

            }
            return newName;
        }

        #endregion

        #region internal static Font GetSystemFont(string family, System.Drawing.FontStyle style, float size)

        /// <summary>
        /// Gets a System.Drawing.Font based upon the family, style and size.
        /// </summary>
        /// <param name="family">The name of the font family</param>
        /// <param name="style">The style of the font</param>
        /// <param name="size">The size of the font</param>
        /// <returns>A new System.Drawing.Font</returns>
        public static Font GetSystemFont(string family, System.Drawing.FontStyle style, float size)
        {
            //Make sure we are initialized and OK
            AssertInitialized();
            var config = ServiceProvider.GetService<IScryberConfigurationService>();
            bool usesystem = config.FontOptions.UseSystemFonts;
            bool usesubstitute = config.FontOptions.FontSubstitution;
            
            if (string.IsNullOrEmpty(family))
                throw new ArgumentNullException("family");

            FontReference fref = _customfamilies[family, style];
            
            if (null == fref && usesystem)
                fref = _systemfamilies[family, style];
            if (null == fref)
                fref = _genericfamilies[family, style];

#if FALLBACKTOREGULAR
            if (null == fref && style != System.Drawing.FontStyle.Regular)
                return GetSystemFont(family, System.Drawing.FontStyle.Regular, size);
#endif
            if (null == fref)
            {
                if (usesubstitute)
                {
                    FamilyReference fam = _customfamilies[family];
                    if (null == fam && usesystem)
                        fam = _systemfamilies[family];

                    if (null != fam)
                        fref = fam.First;

                    //Fallback - use courier font definition
                    if (null == fref)
                    {
                        return new Font(GetSystemFontFamilyNameForStandardFont("Courier"), size, style, GraphicsUnit.Point);
                    }
                }
                else if (usesystem)
                    throw new NullReferenceException(String.Format(Errors.FontNotFound, family + " " + style.ToString()));
                else
                    throw new NullReferenceException(String.Format(Errors.FontNotFoundOrEnableSystem, family + " " + style.ToString()));
            }
            Font f;
            if (null == fref.SystemFamily)
                f = new Font(fref.FamilyName, size, fref.Style, GraphicsUnit.Point);
            else
                f = new Font(fref.SystemFamily, size, fref.Style, GraphicsUnit.Point);
            return f;
        }

        #endregion

        #region internal static PDFFontDefinition GetFontDefinition(PDFFont font)

        public static PDFFontDefinition GetFontDefinition(PDFFont font)
        {
            //Make sure we are initialized and OK
            AssertInitialized();

            System.Drawing.FontStyle fs = font.GetDrawingStyle();
            string family = font.FamilyName;
            return GetFontDefinition(family, fs);

        }

        #endregion

        #region public static bool IsFontDefined(string family, System.Drawing.FontStyle style)
        /// <summary>
        /// Returns true if the available fonts contain one with the specified family and style
        /// </summary>
        /// <param name="family">The name of the font family (case insensitive)</param>
        /// <param name="style">GThe required stlye of the font</param>
        /// <returns>True if available, or false</returns>
        public static bool IsFontDefined(string family, System.Drawing.FontStyle style)
        {
            //Make sure we are initialized and OK
            AssertInitialized();
            var config = ServiceProvider.GetService<IScryberConfigurationService>();
            bool usesystem = config.FontOptions.UseSystemFonts;

            if (string.IsNullOrEmpty(family))
                throw new ArgumentNullException("family");




            FontReference fref = _customfamilies[family, style];

            if (null == fref && usesystem)
                fref = _systemfamilies[family, style];

            if (null == fref)
                fref = _genericfamilies[family, style];

#if FALLBACKTOREGULAR
            if (null == fref && style != System.Drawing.FontStyle.Regular && !PDFFont.IsStandardFontFamily(family))
                return IsFontDefined(family, System.Drawing.FontStyle.Regular);
#endif

            if (null == fref)
            {
                //check if we are a standard font (that is not available in the custom or system families
                if (PDFFont.IsStandardFontFamily(family))
                    return true;
                else
                    return false;
            }
            else
                return true;
        }

        #endregion

        public static string[] GetAllCustomFontFamilies()
        {
            AssertInitialized();
            List<string> all = new List<string>();
            if (_customfamilies != null)
                _customfamilies.FillAllFamilies(all);
            return all.ToArray();
        }

        public static string[] GetAllSystemFontFamilies()
        {
            AssertInitialized();
            List<string> all = new List<string>();
            if (_systemfamilies != null)
                _systemfamilies.FillAllFamilies(all);
            return all.ToArray();


        }

        public static string[] GetAllGenericFontFamilies()
        {
            string[] found = new string[] { "Sans-Serif", "Serif", "Cursive", "Monospace" };
            return found;
        }

        
        public static PDFFontDefinition GetFontDefinition(string fullname)
        {
            int pos = fullname.IndexOf(",");
            System.Drawing.FontStyle fontstyle = System.Drawing.FontStyle.Regular;
            string family;
            if (pos > 0)
            {
                family = fullname.Substring(0, pos).Trim();
                string style = fullname.Substring(pos);
                if (style.IndexOf("bold", StringComparison.OrdinalIgnoreCase) > -1)
                    fontstyle = fontstyle | System.Drawing.FontStyle.Bold;

                if (style.IndexOf("italic", StringComparison.OrdinalIgnoreCase) > -1)
                    fontstyle = fontstyle | System.Drawing.FontStyle.Italic;
            }
            else
                family = fullname.Trim();

            return GetFontDefinition(family, fontstyle);
            
        }

        #region internal static PDFFontDefinition GetFontDefinition(string family, System.Drawing.FontStyle style)

        /// <summary>
        /// Gets the PDFFontDefinition for the specified famil and style
        /// </summary>
        /// <param name="family"></param>
        /// <param name="style"></param>
        /// <returns></returns>
        public static PDFFontDefinition GetFontDefinition(string family, System.Drawing.FontStyle style)
        {
            //Make sure we are initialized and OK
            AssertInitialized();
            var config = ServiceProvider.GetService<IScryberConfigurationService>();
            bool usesystem = config.FontOptions.UseSystemFonts;
            bool usesubstitute = config.FontOptions.FontSubstitution;
            
            if (string.IsNullOrEmpty(family))
                throw new ArgumentNullException("family");

            
            
            
            FontReference fref = _customfamilies[family, style];

            if (null == fref && usesystem)
                fref = _systemfamilies[family, style];
            if (null == fref)
                fref = _genericfamilies[family, style];

            //check if we are a standard font (that is not available in the custom or system families
            if (null == fref && PDFFont.IsStandardFontFamily(family))
                return PDFFontDefinition.LoadStandardFont(family, style);

            if (null == fref)
            {

                if (usesubstitute && style != System.Drawing.FontStyle.Regular && !PDFFont.IsStandardFontFamily(family))
                    return GetFontDefinition(family, System.Drawing.FontStyle.Regular);            

                //We dont have the explicit font so if we should substitue then 
                //try to find the family and return that otherwise use courier.
                if (usesubstitute)
                {
                    FamilyReference fam = _customfamilies[family];
                    if (null == fam && usesystem)
                        fam = _systemfamilies[family];

                    if (null != fam)
                        fref = fam.First;

                    //Fallback - use courier font definition
                    if (null == fref)
                    {
                        return PDFFonts.Courier;
                    }
                }
                else
                    throw new NullReferenceException(String.Format(Errors.FontNotFound, family + " " + style.ToString()));
                
            }

            lock (fref.LoadLock)
            {
                if(fref.LoadedDefintion == false)
                {
                    PDFFontDefinition defn;
                    if (null == fref.FileData)
                        //Load from a file path if we don't have the binary data
                        defn = PDFFontDefinition.LoadOpenTypeFontFile(fref.FilePath, family, style, fref.FileHeadOffset);
                    else
                        //We do have the binary data so load from this
                        defn = PDFFontDefinition.LoadOpenTypeFontFile(fref.FileData, family, style, fref.FileHeadOffset);

                    fref.Definition = defn;
                }
            }
            return fref.Definition;
        }

        #endregion

        //
        // private methods
        //

        #region private static void AssertInitialized()

        /// <summary>
        /// Thread safe intiailization. Must be called before the custom and system families are used
        /// </summary>
        private static void AssertInitialized()
        {
            if (_init == false)
            {
                lock (_initlock)
                {
                    try
                    {
                        //Set init here. We only want to do it once, even if if fails
                        _init = true;
                        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                        _systemfamilies = LoadSystemFonts();
                        _customfamilies = LoadCustomFamilies();
                        _genericfamilies = LoadGenericFamilies();
                        sw.Stop();
                        //System.Diagnostics.Debug.WriteLine("Loaded all system and custom fonts in :" + sw.Elapsed);
                    }
                    catch (Exception ex)
                    {
                        _initex = ex;
                        string msg = String.Format(Errors.CouldNotInitializeTheFonts, ex.Message);
                        throw new System.Configuration.ConfigurationErrorsException(msg, ex);
                    }
                }
            }
            else if (null != _initex)
            {
                string msg = String.Format(Errors.CouldNotInitializeTheFonts, _initex.Message);
                throw new System.Configuration.ConfigurationErrorsException(msg, _initex);
            }
        }

        #endregion

        private static string[] _fontspaths = null;
        private const string FontsDirectoryName = "Fonts";

        private static string[] GetFontsDirectory()
        {
            
            if (null == _fontspaths)
            {
                List<string> paths = new List<string>();

                if (Scryber.Utilities.FrameworkHelper.IsMacOS())
                {
                    paths.Add(System.Environment.GetFolderPath(Environment.SpecialFolder.System) + "/Library/Fonts");
                    paths.Add(System.Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
                }
                else
                {
                    paths.Add(System.Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
                }

                string systempath = System.Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (!systempath.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                    systempath = systempath + System.IO.Path.DirectorySeparatorChar.ToString();
                systempath = systempath + FontsDirectoryName;

                if (!paths.Contains(systempath, StringComparer.OrdinalIgnoreCase))
                    paths.Add(systempath);


                _fontspaths = paths.ToArray();
            }
            return _fontspaths;
        }

        private static FamilyReferenceBag LoadGenericFamilies()
        {
            
            PrivateFontCollection generic = new PrivateFontCollection();
            FamilyReferenceBag genericBag = new FamilyReferenceBag(generic);

            FamilyReference found;
            if (_customfamilies.TryGetFamily("Helvetica", out found) || _customfamilies.TryGetFamily("Arial", out found)
                || _systemfamilies.TryGetFamily("Helvetica", out found) || _systemfamilies.TryGetFamily("Arial", out found)
                || _systemfamilies.TryGetFamily("System Font", out found))
            {
                genericBag.AddFontFamily("Sans-Serif", found);
            }

            if(_customfamilies.TryGetFamily("Times New Roman",out found) || _customfamilies.TryGetFamily("Times", out found)
                || _systemfamilies.TryGetFamily("Times New Roman", out found) || _systemfamilies.TryGetFamily("Times", out found)
                || _systemfamilies.TryGetFamily(".New York", out found))
            {
                genericBag.AddFontFamily("Serif", found);
            }

            if(_customfamilies.TryGetFamily("Courier", out found) || _customfamilies.TryGetFamily("Courier New", out found) || _systemfamilies.TryGetFamily("Courier", out found) || _systemfamilies.TryGetFamily("Courier New", out found))
            {
                genericBag.AddFontFamily("Monospace", found);
            }

            if (_customfamilies.TryGetFamily("Comic Sans MS", out found) || _systemfamilies.TryGetFamily("Comic Sans MS", out found))
            {
                genericBag.AddFontFamily("Cursive", found);
            }

            return genericBag;
        }

        #region private static FamilyReferenceBag LoadSystemFonts()

        /// <summary>
        /// Loads all the system TrueType fonts from the system fonts folder
        /// </summary>
        /// <returns></returns>
        private static FamilyReferenceBag LoadSystemFonts()
        {
            
            InstalledFontCollection install = new InstalledFontCollection();
            FamilyReferenceBag bag = new FamilyReferenceBag(install);
            var config = ServiceProvider.GetService<IScryberConfigurationService>();
            //Check to see if we are allowed to use the system fonts
            if (config.FontOptions.UseSystemFonts)
            {
                try
                {
                    string[] paths = GetFontsDirectory();
                    foreach (var path in paths)
                    {


                        if (!string.IsNullOrEmpty(path))
                        {
                            System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(path);
                            if (dir.Exists)
                            {
                                Scryber.OpenType.TTFRef[] all = Scryber.OpenType.TTFRef.LoadRefs(dir);
                                foreach (Scryber.OpenType.TTFRef item in all)
                                {
                                    if (item.IsValid)
                                    {
                                        bag.AddFontFile(item.FamilyName, GetFontStyle(item.FontSelection), item.FullPath, item.HeadOffset);
                                    }
                                }

                            }
                        }
                    }
                    FontFamily[] installedfamilies = install.Families;
                    foreach (FontFamily fam in installedfamilies)
                    {
                        FamilyReference famref = bag[fam.Name];
                        if (null != famref)
                            famref.SystemFamily = fam;
                    }

                }
                catch (Exception ex)
                {
                    throw new Exception("Could not load the system fonts", ex);
                }
            }
            return bag;
        }

        
        #endregion

        #region private static FamilyReferenceBag LoadCustomFamilies()

        /// <summary>
        /// Loads all the custom fonts definied in the configuration file
        /// </summary>
        /// <returns></returns>
        private static FamilyReferenceBag LoadCustomFamilies()
        {
            PrivateFontCollection priv = new PrivateFontCollection();
            FamilyReferenceBag bag = new FamilyReferenceBag(priv);

            //Load the explicit entries first
            var config = ServiceProvider.GetService<IScryberConfigurationService>();
            var known = config.FontOptions.Register;
            
            if ((null != known) && (known.Length > 0))
            {
                //For each of the configuration entries either load from the resources, or load from the file
                Dictionary<string, System.Resources.ResourceManager> mgrs = new Dictionary<string, System.Resources.ResourceManager>(StringComparer.OrdinalIgnoreCase);

                foreach (var map in known)
                {
                    string family = map.Family;
                    System.Drawing.FontStyle style = map.Style;

                   
                    if (!string.IsNullOrEmpty(map.File))
                    {
                        //Load from a file
                        string path = map.File;

                        try
                        {
                            path = GetFullPath(path);
                            priv.AddFontFile(path);
                        }
                        catch (Exception ex)
                        {
                            throw new ConfigurationErrorsException(String.Format(Errors.CouldNotLoadTheFontFile, path, ex.Message), ex);
                        }

                        ///Only support ttf not ttc
                        bag.AddFontFile(family, style, path, 0);
                    }
                    else
                    {
                        throw new ConfigurationErrorsException(String.Format(Errors.FontMappingMustHaveFilePathOrResourceName, family));
                    }
                }

            }

            string defaultdir = config.FontOptions.DefaultDirectory;

            

            if (!string.IsNullOrEmpty(defaultdir))
            {
                //Make sure we are not just referencing the fonts directory.
                var system = GetFontsDirectory();
                if (Array.IndexOf(system, defaultdir) < 0)
                {
                    System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(defaultdir);

                    if (dir.Exists)
                    {
                        //Load all the fonts from the directory
                        Scryber.OpenType.TTFRef[] all = Scryber.OpenType.TTFRef.LoadRefs(dir);

                        foreach (Scryber.OpenType.TTFRef item in all)
                        {
                            if (item.IsValid)
                            {
                                try
                                {

                                    //try and add the font file
                                    priv.AddFontFile(item.FullPath);
                                }
                                catch (Exception ex)
                                {
                                    throw new ConfigurationErrorsException(String.Format(Errors.CouldNotLoadTheFontFile, item.FullPath, ex.Message), ex);
                                }

                                //font file added  - now register the family and style against the path
                                bag.AddFontFile(item.FamilyName, GetFontStyle(item.FontSelection), item.FullPath, item.HeadOffset);
                            }
                            
                        }

                    }
                }
            }

            FontFamily[] privatefamilies = priv.Families;
            foreach (FontFamily fam in privatefamilies)
            {
                FamilyReference famref = bag[fam.Name];
                if (null != famref)
                    famref.SystemFamily = fam;
            }


            return bag;
        }

        #endregion

        #region private static System.Resources.ResourceManager LoadResourceManager(string basename)

        /// <summary>
        /// Loads a resorce manager for a specific base name on the format 'basename [, assemblyname]'
        /// </summary>
        /// <param name="basename"></param>
        /// <returns></returns>
        /// <remarks>If the assembly name is not provided then the current executing assembly is used</remarks>
        private static System.Resources.ResourceManager LoadResourceManager(string basename)
        {
            //TODO: Check if we are wa web project and load from the web project assembly rather than the executing assembly.
            System.Resources.ResourceManager mgr;
            System.Reflection.Assembly assm;

            try
            {
                if (!string.IsNullOrEmpty(basename))
                {
                    int offset = basename.IndexOf(',');
                    if (offset > 0)
                    {
                        string assmname = basename.Substring(offset + 1).Trim();
                        basename = basename.Substring(0, offset).Trim();

                        assm = System.Reflection.Assembly.Load(assmname);
                    }
                    else
                        assm = System.Reflection.Assembly.GetExecutingAssembly();
                }
                else
                    assm = System.Reflection.Assembly.GetExecutingAssembly();

                mgr = new System.Resources.ResourceManager(basename, assm);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException(String.Format(Errors.CouldNotLoadTheResourceManagerForBase, basename), ex);
            }
            return mgr;
        }

        #endregion

        #region private static string GetFullPath(string path)

        /// <summary>
        /// Gets the full path to a file if it is not rooted
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string GetFullPath(string path)
        {
            if (!System.IO.Path.IsPathRooted(path))
            {
                //if (System.Web.HttpContext.Current != null)
                //    path = System.Web.HttpContext.Current.Server.MapPath(path);
                //else
                path = System.IO.Path.Combine(System.Environment.CurrentDirectory, path);
            }
            return path;
        }

        #endregion

        #region private static System.Drawing.FontStyle GetFontStyle(Scryber.OpenType.SubTables.FontSelection fs)

        /// <summary>
        /// Converts a OpenType.FontSelection to a Drawing.FontStyle
        /// </summary>
        /// <param name="fs"></param>
        /// <returns></returns>
        private static System.Drawing.FontStyle GetFontStyle(Scryber.OpenType.SubTables.FontSelection fs)
        {
            System.Drawing.FontStyle style = System.Drawing.FontStyle.Regular;

            if ((fs & Scryber.OpenType.SubTables.FontSelection.Italic) > 0)
                style |= System.Drawing.FontStyle.Italic;
            if ((fs & Scryber.OpenType.SubTables.FontSelection.Underscore) > 0)
                style |= System.Drawing.FontStyle.Underline;
            if ((fs & Scryber.OpenType.SubTables.FontSelection.Strikeout) > 0)
                style |= System.Drawing.FontStyle.Strikeout;
            if ((fs & Scryber.OpenType.SubTables.FontSelection.Bold) > 0)
                style |= System.Drawing.FontStyle.Bold;

            return style;
        }

        #endregion

    }
}
