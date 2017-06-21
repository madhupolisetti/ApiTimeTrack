using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace ApiTimeTrack
{
    public sealed class ApiHostSectionHandler : IConfigurationSectionHandler
    {

        object IConfigurationSectionHandler.Create(object parent, object configContext, System.Xml.XmlNode section)
        {
            System.Collections.Hashtable configSection = new System.Collections.Hashtable();
            System.Collections.Hashtable rootNodeAttributes = new System.Collections.Hashtable();

            foreach (System.Xml.XmlAttribute attribute in section.Attributes)
                if (attribute.NodeType == System.Xml.XmlNodeType.Attribute)
                    rootNodeAttributes.Add(attribute.Name, attribute.Value);

            configSection.Add(section.Name, rootNodeAttributes);

            foreach (System.Xml.XmlNode childNode in section.ChildNodes)
            {
                if (childNode.NodeType == System.Xml.XmlNodeType.Element)
                {
                    System.Collections.Hashtable childNodeAttributes = new System.Collections.Hashtable();
                    foreach (System.Xml.XmlAttribute attribute in childNode.Attributes)
                        if (attribute.NodeType == System.Xml.XmlNodeType.Attribute)
                            childNodeAttributes.Add(attribute.Name, attribute.Value);
                    configSection.Add(childNode.Name, childNodeAttributes);
                }
            }

            return configSection;
        }
    }
}
