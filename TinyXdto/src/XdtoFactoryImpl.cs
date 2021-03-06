﻿using System;
using ScriptEngine.Machine.Contexts;
using ScriptEngine.Machine;
using ScriptEngine.HostedScript.Library.Xml;
using System.Xml.Schema;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace TinyXdto
{
	[ContextClass("ФабрикаXDTO", "XDTOFactory")]
	public class XdtoFactoryImpl : AutoContext<XdtoFactoryImpl>
	{

		public XdtoFactoryImpl ()
			: this (new XdtoPackageImpl [] { } )
		{
		}

		public XdtoFactoryImpl (IEnumerable<XdtoPackageImpl> packages)
		{
			var _packages = new List<XdtoPackageImpl> ();
			_packages.Add (W3Org.XmlSchema.W3OrgXmlSchemaPackage.Create ());
			foreach (var package in packages) {
				if (!_packages.Contains (package)) {
					_packages.Add (package);
				}
			}

			Packages = new XdtoPackageCollectionImpl (_packages);
		}

		public XdtoFactoryImpl (IEnumerable<XmlSchema> schemas)
		{
			var packages = new List<XdtoPackageImpl> ();
			packages.Add (W3Org.XmlSchema.W3OrgXmlSchemaPackage.Create ());
			foreach (var schema in schemas) {
				var package = new XdtoPackageImpl (schema, this); // TODO: фабрика ещё не сформирована!
				if (!packages.Contains (package)) {
					packages.Add (package);
				}
			}
			Packages = new XdtoPackageCollectionImpl (packages);
		}

		[ContextProperty("Пакеты", "Packages")]
		public XdtoPackageCollectionImpl Packages { get; }

		private void WriteTypeAttribute (XmlWriterImpl xmlWriter,
		                                 IValue value)
		{
			string typeName;
			string typeUri;

			if (value is XdtoDataObjectImpl) {
				var obj = value as XdtoDataObjectImpl;
				typeUri = obj.Type ().NamespaceUri;
				typeName = obj.Type ().Name;
			} else if (value is XdtoDataValueImpl) {
				var obj = value as XdtoDataValueImpl;
				typeUri = obj.Type ().NamespaceUri;
				typeName = obj.Type ().Name;
			} else {
				typeName = "string";
				typeUri = XmlNs.xs;
			}

			var ns = xmlWriter.LookupPrefix (typeUri)?.AsString();
			if (string.IsNullOrEmpty (ns)) {

				// WriteAttribute(name, ns, value) при создании нового префикса
				//  не опознаёт префиксы, записанные через WriteNamespaceMapping
				var prefixIndex = xmlWriter.NamespaceContext.NamespaceMappings ().Count () + 2; // TODO: Костыль с +2
				var prefixDepth = xmlWriter.NamespaceContext.Depth;
				ns = string.Format ("d{0}p{1}", prefixDepth, prefixIndex);
				xmlWriter.WriteNamespaceMapping (ns, typeUri);
			}
			xmlWriter.WriteAttribute ("type", XmlNs.xsi, string.Format ("{0}:{1}", ns, typeName));
		}

		private void WriteXdtoSequence (XmlWriterImpl xmlWriter,
		                                XdtoSequenceImpl sequence)
		{
			foreach (var element in sequence) {

				if (element == null) {

					// TODO: надо ли что-нибудь делать???

				} else if (element is XdtoSequenceStringElement) {

					xmlWriter.WriteText ((element as XdtoSequenceStringElement).Text);

				} else if (element is XdtoSequenceValueElement) {

					var obj = element as XdtoSequenceValueElement;
					WriteXml (xmlWriter, obj.Value,
					          obj.Property.LocalName, obj.Property.NamespaceURI,
					          XmlTypeAssignmentEnum.Explicit,
					          obj.Property.Form);

				}
			}
		}

		private void WriteXdtoObject (XmlWriterImpl xmlWriter,
		                              XdtoDataObjectImpl obj)
		{
			obj.Validate ();

			if (obj.Type ().Sequenced) {
				WriteXdtoSequence (xmlWriter, obj.Sequence ());
				return;
			}

			foreach (var property in obj.Properties()) {

				var typeAssignment = XmlTypeAssignmentEnum.Explicit;

				var value = obj.Get (property) as IXdtoValue;
				WriteXml (xmlWriter, value,
				          property.LocalName,
				          property.NamespaceURI,
				          typeAssignment,
				          property.Form);
			}
		}

		[ContextMethod ("ЗаписатьXML", "WriteXML")]
		public void WriteXml (XmlWriterImpl xmlWriter,
		                      IXdtoValue value,
		                      string localName,
		                      string namespaceUri = null,
		                      XmlTypeAssignmentEnum? typeAssignment = null,
		                      XmlFormEnum? xmlForm = null)
		{
			xmlForm = xmlForm ?? XmlFormEnum.Element;
			typeAssignment = typeAssignment ?? XmlTypeAssignmentEnum.Implicit;

			if (xmlForm == XmlFormEnum.Element) {

				xmlWriter.WriteStartElement (localName, namespaceUri);

				if (typeAssignment == XmlTypeAssignmentEnum.Implicit) {
					WriteTypeAttribute (xmlWriter, value);
				}

				if (value == null || value.DataType == DataType.Undefined) {
					
					xmlWriter.WriteAttribute ("nil", XmlNs.xsi, "true");

				} else if (value is XdtoDataObjectImpl) {
					
					WriteXdtoObject (xmlWriter, value as XdtoDataObjectImpl);

				} else if (value is XdtoDataValueImpl) {

					var dataValue = value as XdtoDataValueImpl;
					xmlWriter.WriteText (dataValue.LexicalValue);

				} else {
					
					xmlWriter.WriteText (value.ToString ());

				}

				xmlWriter.WriteEndElement ();

			} else if (xmlForm == XmlFormEnum.Attribute) {

				xmlWriter.WriteAttribute (localName, namespaceUri, value.AsString ());

			} else if (xmlForm == XmlFormEnum.Text) {

				xmlWriter.WriteText (value.AsString ()); // TODO: XmlString ??

			} else
				throw new NotSupportedException ("Какой-то новый тип для XML");
		}

		[ContextMethod ("Тип", "Type")]
		public IXdtoType Type (string uri, string name)
		{

			var package = Packages.Get (uri);
			if (package == null)
				return null;

			var type = package.FirstOrDefault((t) => t.Name.Equals(name, StringComparison.Ordinal));
			return type;
		}

		public IXdtoType Type (XmlQualifiedName xmlType)
		{
			return Type (new XmlDataType (xmlType.Name, xmlType.Namespace));
		}

		[ContextMethod("Тип", "Type")]
		public IXdtoType Type (IValue xmlType)
		{
			if (xmlType is XmlDataType)
				return Type ((xmlType as XmlDataType).NamespaceUri, (xmlType as XmlDataType).TypeName);

			if (xmlType is XmlExpandedName)
				return Type ((xmlType as XmlExpandedName).NamespaceUri, (xmlType as XmlExpandedName).LocalName);

			throw RuntimeException.InvalidArgumentType (nameof (xmlType));
		}

		[ContextMethod ("Создать", "Create")]
		public IXdtoValue Create (IXdtoType type, IValue value = null)
		{
			if (type is XdtoObjectTypeImpl) {
				return new XdtoDataObjectImpl (type as XdtoObjectTypeImpl, null, null);
			}

			var valueType = type as XdtoValueTypeImpl;
			if (valueType == null) {
				throw RuntimeException.InvalidArgumentType (nameof (type));
			}

			if (value == null) {
				throw RuntimeException.InvalidArgumentType (nameof (value));
			}

			if (value.DataType == DataType.String) {
				return new XdtoDataValueImpl (valueType,
				                              value.AsString (),
				                              value);
			}

			return new XdtoDataValueImpl (valueType,
			                              value.AsString (),
			                              value);
		}

		public IXdtoValue ReadXml (XmlReaderImpl reader, IXdtoType type = null)
		{
			reader.Read ();
			if (type == null) {
				var explicitType = reader.GetAttribute (ValueFactory.Create ("type"), XmlNs.xsi);
				if (explicitType.DataType == DataType.Undefined) {

					type = new XdtoObjectTypeImpl ();

				} else {

					var defaultNamespace = reader.NamespaceContext.DefaultNamespace;

					// Задан тип - пытаемся его распознать
					var sType = explicitType.AsString ();
					var nameElements = sType.Split (':');

					var typeUri = nameElements.Count () > 1
					                          ? nameElements [0]
					                          : defaultNamespace;
					var typeName = nameElements.Count () > 1
					                          ? nameElements [1]
					                          : nameElements [0];

					// TODO: namespace context :'(
					var nsMapping = reader.AttributeValue (ValueFactory.Create(string.Format("xmlns:{0}", typeUri)));
					if (nsMapping != null && nsMapping.DataType == DataType.String) {
						typeUri = nsMapping.AsString ();
					}

					type = this.Type (typeUri, typeName);
				}
			}

			if (type is XdtoObjectTypeImpl) {
				return type.Reader.ReadXml (reader, type, this);
			}

			var xmlNodeTypeEnum = XmlNodeTypeEnum.CreateInstance ();
			var xmlElementStart = xmlNodeTypeEnum.FromNativeValue (XmlNodeType.Element);
			if (type is XdtoValueTypeImpl) {
				if (reader.NodeType.Equals (xmlElementStart)) {
					reader.Read ();
				}
				var result = type.Reader.ReadXml (reader, type, this);
				reader.Skip ();
				return result;
			}

			throw new NotSupportedException ("Неожиданный тип XDTO!");
		}

		[ScriptConstructor]
		static public IRuntimeContextInstance Constructor ()
		{
			return new XdtoFactoryImpl ();
		}
	}
}

