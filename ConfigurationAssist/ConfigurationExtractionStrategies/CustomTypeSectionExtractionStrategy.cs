﻿using System;
using System.Configuration;
using System.Linq;
using System.Xml;
using ConfigurationAssist.Common;
using ConfigurationAssist.CustomAttributes;
using ConfigurationAssist.Interfaces;

namespace ConfigurationAssist.ConfigurationExtractionStrategies
{
    public class CustomTypeSectionExtractionStrategy : IConfigurationExtractionStrategy
    {
        private readonly Conversion _converter;

        public CustomTypeSectionExtractionStrategy(string fullSectionName) : this()
        {
            FullSectionName = fullSectionName;
        }

        public CustomTypeSectionExtractionStrategy()
        {
            _converter = new Conversion();
        }

        public string FullSectionName { get; set; }
        
        public T ExtractConfiguration<T>() where T : class, new()
        {
            return ExtractConfigurationSection<T>();
        }

        private T ExtractConfigurationSection<T>() where T : class, new()
        {
            if (string.IsNullOrEmpty(FullSectionName))
            {
                FullSectionName = GetConfigurationSectionName(typeof(T));
            }

            var configuration = (T)ConfigurationManager.GetSection(FullSectionName);
            if (configuration == null)
            {
                throw new ConfigurationErrorsException(string.Format("Could not convert the named section '{0}' to type '{1}'",
                    FullSectionName,
                    typeof(T)));
            }

            var baseType = configuration as ConfigurationSection;
            if (baseType == null)
            {
                throw new ConfigurationErrorsException(
                    string.Format("The strategy exectuted for {0} is not inherited from type ConfigurationSection",
                        typeof(T).Name));
            }

            
            var xdoc = new XmlDocument();
            xdoc.Load(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
            var xnode = xdoc.SelectSingleNode(string.Format("/configuration/{0}", FullSectionName));
            var output = Activator.CreateInstance(typeof (T));
            ExtractNodeToObject(xnode, output, typeof (T));

            return output as T;
        }

        private void ExtractNodeToObject(XmlNode node, object output, Type type)
        {
            if (node == null)
            {
                return;
            }
            
            var properties = type.GetProperties();
            foreach (var property in properties)
            {
                string keyName;
                
                var attribute = property.GetCustomAttributes(typeof (ConfigurationPropertyAttribute), true);
                if (!attribute.Any())
                {
                    keyName = property.Name;
                }
                else
                {
                    var propertyAttribute = (ConfigurationPropertyAttribute)attribute.First();
                    keyName = propertyAttribute.Name;
                }

                if (node.Attributes != null && node.Attributes[keyName] != null)
                {
                    var convertedValue = _converter.Convert(property.PropertyType, node.Attributes[keyName].Value);
                    property.SetValue(output, convertedValue, null);
                    continue;
                }

                if (property.PropertyType.BaseType != typeof (ConfigurationElement) ||
                    node.SelectSingleNode(keyName) == null)
                {
                    continue;
                }

                var obj = Activator.CreateInstance(property.PropertyType);
                ExtractNodeToObject(node.SelectSingleNode(keyName), obj, property.PropertyType);
                property.SetValue(output, obj, null);
            }
        }
        
        private string GetConfigurationSectionName(Type type)
        {
            var attr = type.GetCustomAttributes(typeof(ConfigurationSectionItem), true).AsQueryable();
            if (!attr.Any())
            {
                return type.Name;
            }

            var section = (ConfigurationSectionItem)attr.First();
            var sectionName = string.Empty;
            if (!string.IsNullOrEmpty(section.SectionName))
            {
                sectionName = section.SectionName;
            }

            if (!string.IsNullOrEmpty(section.SectionGroup))
            {
                sectionName = string.Format("{0}/{1}", section.SectionGroup, sectionName);
            }

            return sectionName;
        }
    }
}
