/* 
 * Copyright (c) 2016, Firely (info@fire.ly) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/ewoutkramer/fhir-net-api/master/LICENSE
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Specification;

namespace Hl7.Fhir.ElementModel
{
    internal class PocoElementNavigator : IAnnotated
    {
        private Base _parent;

        private int? _arrayIndex; // this is only for the ShortPath implementation eg Patient.Name[1].Family (its the 1 here)
        private int _index;
        private IList<ElementValue> _children;

        // For Normal element properties representing a FHIR type
        internal PocoElementNavigator(Base parent)
        {
            // The root is the special case, we start with a "collection" of children where the parent is the only element
            _parent = null;
            _index = 0;
            _arrayIndex = null;
            _children = new List<ElementValue>() { new ElementValue(parent.TypeName, parent) };

            var typeInfo = (new PocoStructureDefinitionSummaryProvider()).Provide(parent.TypeName);
            DefinitionSummary = Specification.ElementDefinitionSummary.ForRoot(parent.TypeName, typeInfo);
        }

        private PocoElementNavigator()
        {
            // only use for clone
        }

        internal PocoElementNavigator Clone() =>
            new PocoElementNavigator
            {
                _parent = this._parent,
                _arrayIndex = this._arrayIndex,
                _index = this._index,
                _children = this._children,
                ParentClassMapping = this.ParentClassMapping,
                DefinitionSummary = this.DefinitionSummary
            };


        private bool enter(Base parent)
        {
            var children = parent.NamedChildren.ToList();
            if (!children.Any()) return false;

            _parent = parent;
            _children = children;
            ParentClassMapping = PocoStructureDefinitionSummaryProvider.GetMappingForType(parent.GetType());
            
            // Reset everything, next() will initialize the values for the first "child"
            _index = -1;
            _arrayIndex = null;

            return true;
        }


        private bool next(string name = null)
        {
            // If not a collection, or out of collection members, scan
            // for next property
            var scan = _index;

            while (scan + 1 < _children.Count)
            {
                var oldElementName = scan >= 0 ? _children[scan].ElementName : null;

                scan += 1;
                var scanProp = _children[scan];

                if (oldElementName != scanProp.ElementName)
                    _arrayIndex = null;

                if (name == null || scanProp.ElementName == name)
                {
                    _index = scan;

                    var propMapping = ParentClassMapping.FindMappedElementByName(Name);
                    DefinitionSummary = new PocoElementSerializationInfo(propMapping);

                    if (!DefinitionSummary.IsCollection)
                        _arrayIndex = null;
                    else
                        _arrayIndex = _arrayIndex == null ? 0 : _arrayIndex + 1;

                    return true;
                }
            }

            return false;
        }

        internal ElementValue Current => _children[_index];

        public string Name => Current.ElementName;

        public int ArrayIndex => DefinitionSummary.IsCollection ? _arrayIndex.Value : 0;

        internal ClassMapping ParentClassMapping { get; private set; }
        internal IElementDefinitionSummary DefinitionSummary { get; private set; }

        /// <summary>
        /// This is only needed for search data extraction (and debugging)
        /// to be able to read the values from the selected node (if a coding, so can get the value and system)
        /// </summary>
        public Base FhirValue => Current.Value as Base;    // conversion will return null if on id, value, url (primitive attribute props in xml)

        public object Value
        {
            get
            {
                if (Current.Value is string)
                    return Current.Value;

                try
                {
                    switch (Current.Value)
                    {
                        case Hl7.Fhir.Model.Instant ins:
                            return ins.ToPartialDateTime();
                        case Hl7.Fhir.Model.Time time:
                            return time.ToTime();
                        case Hl7.Fhir.Model.Date dt:
                            return dt.ToPartialDateTime();
                        case FhirDateTime fdt:
                            return fdt.ToPartialDateTime();
                        case Hl7.Fhir.Model.Integer fint:
                            return (long)fint.Value;
                        case Hl7.Fhir.Model.PositiveInt pint:
                            return (long)pint.Value;
                        case Hl7.Fhir.Model.UnsignedInt unsint:
                            return (long)unsint.Value;
                        case Hl7.Fhir.Model.Base64Binary b64:
                            return b64.Value != null ? PrimitiveTypeConverter.ConvertTo<string>(b64.Value) : null;
                        case Primitive prim:
                            return prim.ObjectValue;
                        default:
                            return null;
                    }
                }
                catch (FormatException)
                {
                    // If it fails, just return the unparsed shit
                    // Todo: add sentinel class!
                    return (Current.Value as Primitive)?.ObjectValue;
                }
            }
        }

        public string TypeName
        {
            get
            {
                if (Current.Value is string)
                {
                    if (Name == "url")
                        return "uri";
                    else if (Name == "id")
                        return "id";
                    else if (Name == "div")
                        return "xhtml";
                    else
                        throw new NotSupportedException($"Don't know about primitive with name '{Name}'");
                }
                else if(Current.Value is IBackboneElement)
                {
                    return Current.Value is BackboneElement ? "BackboneElement" : "Element";
                }
                else
                {
                    // _currentValue must now be of type Base....
                    var tn = FhirValue.TypeName;

                    if (ModelInfo.IsProfiledQuantity(tn)) tn = "Quantity";

                    return tn;
                }
            }
        }

        private readonly object lockObject = new object();

        public bool MoveToFirstChild(string name = null)
        {
            lock (lockObject)
            {
                // If this is a primitive, there are no children
                if (!(Current.Value is Base b)) return false;

                if (enter(b))
                    return next(name);
                else
                    return false;
            }
        }

        public bool MoveToNext(string name = null)
        {
            return next(name);
        }

        public IEnumerable<object> Annotations(Type type)
        {
            if (FhirValue is IAnnotated ia)
                return ia.Annotations(type);
            else
                return Enumerable.Empty<object>();
        }
    }
}