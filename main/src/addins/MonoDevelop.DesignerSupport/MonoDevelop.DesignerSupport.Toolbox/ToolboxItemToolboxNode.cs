 /* 
 * ToolboxItemToolboxNode.cs - A ToolboxNode that wraps a System.Drawing.Design.ToolboxItem
 * 
 * Authors: 
 *  Michael Hutchinson <m.j.hutchinson@gmail.com>
 *  
 * Copyright (C) 2006 Michael Hutchinson
 *
 * This sourcecode is licenced under The MIT License:
 * 
 * Permission is hereby granted, free of charge, to any person obtaining
 * a copy of this software and associated documentation files (the
 * "Software"), to deal in the Software without restriction, including
 * without limitation the rights to use, copy, modify, merge, publish,
 * distribute, sublicense, and/or sell copies of the Software, and to permit
 * persons to whom the Software is furnished to do so, subject to the
 * following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
 * OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN
 * NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
 * OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE
 * USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System;
using System.IO;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;

using MonoDevelop.Core.Serialization;
using MonoDevelop.Core;

namespace MonoDevelop.DesignerSupport.Toolbox
{
	[Serializable]
	public class ToolboxItemToolboxNode : TypeToolboxNode
	{
		[ItemProperty ("itemtype")]
		TypeReference toolboxItemType;
		
		// The constructors taking a System.Drawing.Design.ToolboxItem, GetToolboxItem () and the
		// BinaryFormatter (de)serialisation of custom ToolboxItems are removed: ToolboxItem is
		// Windows Forms only on .NET and BinaryFormatter is gone (ADR 0009). The node still loads
		// from saved toolbox data (a serialised custom item, "itemcontents", is ignored).
		
		//for deserialisation
		public ToolboxItemToolboxNode ()
		{
		}
		
		public override bool Equals (object obj)
		{
			ToolboxItemToolboxNode other = obj as ToolboxItemToolboxNode;
			return (other != null)
			    && (this.toolboxItemType == null?
				    other.toolboxItemType == null
				    : this.toolboxItemType.Equals (other.toolboxItemType))
			    && base.Equals (other);
		}
		
		public override int GetHashCode ()
		{
			int code = base.GetHashCode ();
			if (toolboxItemType != null)
				code ^= toolboxItemType.GetHashCode ();
			return code;
		}
		
		public override string ItemDomain {
			get { return GettextCatalog.GetString ("Web and Windows Forms Components"); }
		}
	}	
}
