/* 
 * Copyright (c) 2016, Firely (info@fire.ly) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/ewoutkramer/fhir-net-api/master/LICENSE
 */

using System;
using System.Linq;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Navigation;

namespace Hl7.Fhir.Validation
{

    internal static class ElementDefinitionNavigatorExtensions
    {
        public static string GetFhirPathConstraint(this ElementDefinition.ConstraintComponent cc)
        {
            // This was required for 3.0.0, but was rectified in the 3.0.1 technical update
            //if (cc.Key == "ele-1")
            //    return "(children().count() > id.count()) | hasValue()";
            return cc.Expression;
        }

        public static bool IsPrimitiveValueConstraint(this ElementDefinition ed) =>
                   ed.Path.EndsWith(".value") && IsPrimitiveConstraint(ed);

        // EK 20190109 BUG: Our snapshot generator, when constraining .value elements, does not bring in the
        // original extensions/empty code element - so in profiles that constraint .value, this check does not
        // recognize primitive constraints anymore. The commented out code is what I would like to have when
        // this gets fixed.
        public static bool IsPrimitiveConstraint(this ElementDefinition ed) =>
            ed.Representation.Any() ?
                (ed.Representation.Contains(ElementDefinition.PropertyRepresentation.XmlAttr) ||
                 ed.Representation.Contains(ElementDefinition.PropertyRepresentation.Xhtml))
            : false;
            // ed.Type.Any() 
            //&& ed.Type.First().CodeElement != null 
            //&& ed.Type.First().CodeElement.GetExtension("http://hl7.org/fhir/StructureDefinition/structuredefinition-xml-type") != null;


        internal static bool IsResourcePlaceholder(this ElementDefinition ed)
        {
            if (ed.Type == null) return false;
            return ed.Type.Any(t => t.Code == "Resource" || t.Code == "DomainResource");
        }

        public static string ConstraintDescription(this ElementDefinition.ConstraintComponent cc)
        {
            var desc = cc.Key;

            if (cc.Human != null)
                desc += " \"" + cc.Human + "\"";

            return desc;
        }


        public static string QualifiedDefinitionPath(this ElementDefinitionNavigator nav)
        {
            string path = "";

            if (nav.StructureDefinition != null && nav.StructureDefinition.Url != null)
                path = "{" + nav.StructureDefinition.Url + "}";

            path += nav.Path;

            return path;
        }
    }

}