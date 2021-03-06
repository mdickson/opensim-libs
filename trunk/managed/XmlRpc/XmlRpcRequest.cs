using System;
using System.Collections;
using System.IO;
using System.Xml;
using System.Net;
using System.Net.Security;
using System.Text;

namespace Nwc.XmlRpc
{
    /// <summary>Class supporting the request side of an XML-RPC transaction.</summary>
    public class XmlRpcRequest
    {
        private String m_methodName = null;
        private Encoding m_encoding = new UTF8Encoding();
        private XmlRpcRequestSerializer _serializer = new XmlRpcRequestSerializer();
        private XmlRpcResponseDeserializer _deserializer = new XmlRpcResponseDeserializer();

        /// <summary><c>ArrayList</c> containing the parameters.</summary>
        protected IList _params = null;

        /// <summary>Instantiate an <c>XmlRpcRequest</c></summary>
        public XmlRpcRequest()
        {
            _params = new ArrayList();
        }

        /// <summary>Instantiate an <c>XmlRpcRequest</c> for a specified method and parameters.</summary>
        /// <param name="methodName"><c>String</c> designating the <i>object.method</i> on the server the request
        /// should be directed to.</param>
        /// <param name="parameters"><c>ArrayList</c> of XML-RPC type parameters to invoke the request with.</param>
        public XmlRpcRequest(String methodName, IList parameters)
        {
            MethodName = methodName;
            _params = parameters;
        }

        /// <summary><c>ArrayList</c> conntaining the parameters for the request.</summary>
        public virtual IList Params
        {
            get
            {
                return _params;
            }
        }

        /// <summary><c>String</c> conntaining the method name, both object and method, that the request will be sent to.</summary>
        public virtual String MethodName
        {
            get
            {
                return m_methodName;
            }
            set
            {
                m_methodName = value;
            }
        }

        /// <summary><c>String</c> object name portion of the method name.</summary>
        public String MethodNameObject
        {
            get
            {
                int index = MethodName.IndexOf(".");

                if (index == -1)
                    return MethodName;

                return MethodName.Substring(0, index);
            }
        }

        /// <summary><c>String</c> method name portion of the object.method name.</summary>
        public String MethodNameMethod
        {
            get
            {
                int index = MethodName.IndexOf(".");

                if (index == -1)
                    return MethodName;

                return MethodName.Substring(index + 1, MethodName.Length - index - 1);
            }
        }

        /// <summary>Invoke this request on the server.</summary>
        /// <param name="url"><c>String</c> The url of the XML-RPC server.</param>
        /// <returns><c>Object</c> The value returned from the method invocation on the server.</returns>
        /// <exception cref="XmlRpcException">If an exception generated on the server side.</exception>
        public Object Invoke(String url, RemoteCertificateValidationCallback certCallBack = null)
        {
            XmlRpcResponse res = Send(url, 100000, certCallBack);

            if (res.IsFault)
                throw new XmlRpcException(res.FaultCode, res.FaultString);

            return res.Value;
        }

        /// <summary>Send the request to the server.</summary>
        /// <param name="url"><c>String</c> The url of the XML-RPC server.</param>
        /// <returns><c>XmlRpcResponse</c> The response generated.</returns>
        public XmlRpcResponse Send(String url, int timeout = 100000, RemoteCertificateValidationCallback certCallBack = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            if (request == null)
                throw new XmlRpcException(XmlRpcErrorCodes.TRANSPORT_ERROR,
                              XmlRpcErrorCodes.TRANSPORT_ERROR_MSG + ": Could not create request with " + url);
            request.Method = "POST";
            request.ContentType = "text/xml";
            request.AllowWriteStreamBuffering = true;
            request.Timeout = timeout;
            if(certCallBack != null)
                request.ServerCertificateValidationCallback = certCallBack;

            using (Stream stream = request.GetRequestStream())
            using (XmlTextWriter xml = new XmlTextWriter(stream, m_encoding))
            {
                _serializer.Serialize(xml, this);
                xml.Flush();
            }

            XmlRpcResponse resp;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader input = new StreamReader(response.GetResponseStream()))
                resp = (XmlRpcResponse)_deserializer.Deserialize(input);
            return resp;
        }

        /// <summary>Produce <c>String</c> representation of the object.</summary>
        /// <returns><c>String</c> representation of the object.</returns>
        override public String ToString()
        {
            return _serializer.Serialize(this);
        }
    }
}
