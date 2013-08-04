using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.IO;

namespace SilentOrbit.Parsing
{
	public class HtmlCleaner : TagParser
	{
		/// <summary>
		/// Tag names allowed
		/// </summary>
		static readonly List<string> whiteList = new List<string>()
		{
			"a", "abbr", "address", "area", "article", "aside", 
			"b", "bdi", "bdo", "blockquote", "br",
			"canvas", "caption", "cite", "code", "col", "colgroup",
			"dd", "del", "details", "dfn", "div", "dl", "dt",
			"em", 
			"fieldset", "figcaption", "figure", "footer",
			"h1", "h2", "h3", "h4", "h5", "h6", "header", "hr",
			"i", "img", "ins",
			"kbd",
			"label", "legend", "li",
			"map", "mark", "menu", "menuitem", "meter",
			"nav",
			"ol", "output",
			"p", "param", "pre", "progress",
			"q",
			"rp", "rt", "ruby",
			"s", "samp", "section", "small", "span", "strong", "sub", "summary", "sup",
			"table", "tbody", "td", "tfoot", "th", "thead", "time", "tr",
			"u", "ul",
			"var", "wbo",
		};
		//Valid but escaped tags:
		//button
		/// <summary>
		/// These tags are not rendered but their children are
		/// </summary>
		static readonly List<string> hiddenTags = new List<string>()
		{
			//Body tags
			"?xml", "!doctype", "body", "html", "meta", "title",
			//Obsolete
			"acronym", "basefont", "bgsound", "big", "blink",
			"center", "dir", "font", "frame", "frameset", "hgroup",
			"isindex", "listing", "marquee", "nobr", "noframes",
			"plaintext", "spacer", "strike", "tt", "xmp",
			//Blocked Form
			"datalist", "form", "input", "keygen", "optgroup", "option", "select", "textarea",
			//Blocked media
			"audio", "source", "video", "embed", "iframe", "object", "track",
			//Blocked other
			"base", "link", "main", 
			//Hidden since they are not usable
			"data", "noscript",
		};
		/// <summary>
		/// Neither these tags nor their children are rendered
		/// </summary>
		static readonly List<string> hiddenTagsChildren = new List<string>()
		{
			"applet", "head", "script", "style",
		};

		enum TagVisibility
		{
			/// <summary>
			/// Render the tag text as text
			/// </summary>
			Escaped = 0,
			/// <summary>
			/// Render the tag as html
			/// </summary>
			Html = 1,
			/// <summary>
			/// Don't render the tag but do render its children
			/// </summary>
			HiddenTag = 2,
			/// <summary>
			/// Don't render tag or its children
			/// </summary>
			Hidden = 3,
		}

		readonly TextWriter writer;

		/// <summary>
		/// Top tag that is hidden,
		/// Nothing will be written until we reach the matching endtag
		/// </summary>
		Tag hidden = null;

		HtmlCleaner(string raw, TextWriter writer) : base(raw)
		{
			this.writer = writer;
		}

		public static void Clean(string raw, TextWriter writer)
		{
			var c = new HtmlCleaner(raw, writer);
			c.Parse();
		}

		protected override void ParsedAttribute(Tag tag, TagNamespace ns, string key, string val)
		{
			key = key.ToLowerInvariant();
			if (tag.Attributes.ContainsKey(key))
				return;
			val = HttpUtility.HtmlDecode(val);
			switch(key)
			{
				case "href":
					if (!Uri.IsWellFormedUriString(val, UriKind.Absolute))
						return;
					tag.Attributes.Add("href", new Uri(val).ToString());
					return;

					case "src":
					if (tag.Name != "img")
						return;
					if (!Uri.IsWellFormedUriString(val, UriKind.Absolute))
						return;
					tag.Attributes.Add("src", new Uri(val).ToString());
					return;

					case "alt":
					case "align":
					case "border":
					case "cite":
					case "coords":
					case "datetime":
					case "download":
					case "for":
					case "headers":
					case "height":
					case "high":
					case "hreflang":
					case "low":
					case "media":
					case "min":
					case "max":
					case "open":
					case "optimum":
					case "pubdate":
					case "rel":
					case "reversed":
					case "shape":
					case "span":
					case "start":
					case "summary":
					case "title":
					case "type":
					case "value":
					case "width":
					case "wrap":
					tag.Attributes.Add(key, val);
					return;

					case "target":
					switch(val)
					{
						case "_self":
							case "_blank":
							//case "_parent":
							case "_top":
							tag.Attributes.Add(key, val);
							return;
					}
					return;

					case "cols":
					case "colspan":
					case "rows":
					case "rowspan":
					//Integer values
					int iv;
					if (int.TryParse(val, out iv))
						tag.Attributes.Add(key, iv.ToString());
					return;
			}
		}

		/// <summary>
		/// Possibly selfclosed
		/// </summary>
		/// <param name="tag">Tag.</param>
		protected override void ParsedOpeningTag(Tag tag)
		{
			string name = tag.Name;
			switch(name)
			{
				case "hr":
					case "br":
					case "img":
					case "?xml":
					case "!doctype":
					tag.SelfClosed = true;
					break;
			}

			if (hidden != null)
				return;

			bool escaped = true;
			if (whiteList.Contains(name))
				escaped = false;
			else if (hiddenTags.Contains(name))
				return;
			else if (hiddenTagsChildren.Contains(name))
			{
				hidden = tag;
				return;
			}

			if (escaped)
			{
				writer.Write("&lt;");
				writer.Write(tag.Name);
				if (tag.SelfClosed)
					writer.Write("/");
				writer.Write("&gt;");
				return;
			}

			if (tag.Name == "img")
			{
				if (!tag.Attributes.ContainsKey("src"))
					return;

				string url = HttpUtility.HtmlEncode(tag.Attributes["src"]);
				writer.Write("[<a");
				foreach(var a in tag.Attributes)
				{
					if (a.Key == "src")
						continue;
					writer.Write(" " + a.Key + "=\"" + HttpUtility.HtmlEncode(a.Value) + "\"");
				}
				writer.Write(">");
				if (tag.Attributes.ContainsKey("title"))
					writer.Write(HttpUtility.HtmlEncode(tag.Attributes["title"]));
				else
					writer.Write("img");
				writer.Write("</a>: <a href=\"" + url + "\">" + url + "</a>]");
				return;
			}

			//Allowed, HTML
			writer.Write("<");
			writer.Write(tag.Name);
			foreach(var a in tag.Attributes)
				writer.Write(" " + a.Key + "=\"" + HttpUtility.HtmlEncode(a.Value) + "\"");
			if (tag.SelfClosed)
				writer.Write("/");
			writer.Write(">");
		}

		protected override void ParsedClosingTag(Tag tag)
		{
			if (hidden != null)
			{
				//Closing hidden tag, time to show tags again
				if (hidden == tag)
					hidden = null;
				return;
			}

			string name = tag.Name;

			bool escaped = true;
			if (whiteList.Contains(name))
				escaped = false;
			else if (hiddenTags.Contains(name))
				return;
			else if (hiddenTagsChildren.Contains(name))
			{
				Console.Error.WriteLine("Unexpected hidden closing tag: " + name);
				return;
			}

			if (escaped)
			{
				writer.Write("&lt;/");
				writer.Write(tag.Name);
				writer.Write("&gt;");
			}
			else
			{
				writer.Write("</");
				writer.Write(tag.Name);
				writer.Write(">");
			}
		}

		protected override void ParsedText(string decodedText)
		{
			if (hidden != null)
				return;

			writer.Write(HttpUtility.HtmlEncode(decodedText));
		}
	}
}

