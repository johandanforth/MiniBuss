using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Messages
{
    public class Message<T>
    {
        public string ToXml()
        {
            var xmlWriterSettings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false,
                Encoding = Encoding.UTF8
            };

            using (var ms = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(ms, xmlWriterSettings))
                {
                    var serializer = new XmlSerializer(typeof (T));
                    serializer.Serialize(writer, this);
                    writer.Flush();
                    ms.Position = 0;
                }
                using (var rdr = new StreamReader(ms))
                {
                    var xml = rdr.ReadToEnd();
                    return xml;
                }
            }
        }
    }
}