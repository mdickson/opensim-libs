using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HttpServer
{
    /// <summary>
    /// Response that is sent back to the web browser / client.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A response can be sent if different ways. The easiest one is
    /// to just fill the Body stream with content, everything else
    /// will then be taken care of by the framework. The default content-type
    /// is text/html, you should change it if you send anything else.
    /// </para><para>
    /// The second and slightly more complex way is to send the response
    /// as parts. Start with sending the header using the SendHeaders method and 
    /// then you can send the body using SendBody method, but do not forget
    /// to set <see cref="ContentType"/> and <see cref="ContentLength"/> before doing so.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Example using response body.
    /// class MyModule : HttpModule
    /// {
    /// 		public override bool Process(IHttpRequest request, IHttpResponse response, IHttpSession session)
    /// 		{
    /// 			StreamWriter writer = new StreamWriter(response.Body);
    /// 			writer.WriteLine("Hello dear World!");
    /// 			writer.Flush();
    /// 
    /// 			// return true to tell webserver that we've handled the url
    /// 			return true;
    /// 		}
    /// }
    /// </code>
    /// </example>
    /// todo: add two examples, using SendHeaders/SendBody and just the Body stream.
    public class HttpResponse : IHttpResponse
    {
        private const string DefaultContentType = "text/html;charset=UTF-8";
        private readonly IHttpClientContext _context;
        private readonly ResponseCookies _cookies = new ResponseCookies();
        private readonly NameValueCollection _headers = new NameValueCollection();
        private string _httpVersion;
        private Stream _body = new MemoryStream();
        private long _contentLength;
        private string _contentType;
        private bool _contentTypeChangedByCode;
        private Encoding _encoding = Encoding.UTF8;
        private int _keepAlive = 60;
        public uint requestID { get; private set; }
        public byte[] RawBuffer { get; set; }
        public int RawBufferStart { get; set; }
        public int RawBufferLen { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="IHttpResponse"/> class.
        /// </summary>
        /// <param name="context">Client that send the <see cref="IHttpRequest"/>.</param>
        /// <param name="request">Contains information of what the client want to receive.</param>
        /// <exception cref="ArgumentException"><see cref="IHttpRequest.HttpVersion"/> cannot be empty.</exception>
        public HttpResponse(IHttpClientContext context, IHttpRequest request)
        {
            Check.Require(context, "context");
            Check.Require(request, "request");

            _httpVersion = request.HttpVersion;
            if (string.IsNullOrEmpty(_httpVersion))
                throw new ArgumentException("HttpVersion in IHttpRequest cannot be empty.");

            Status = HttpStatusCode.OK;
            _context = context;
            m_Connetion = request.Connection;
            requestID = request.ID;
            RawBufferStart = -1;
            RawBufferLen = -1;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IHttpResponse"/> class.
        /// </summary>
        /// <param name="context">Client that send the <see cref="IHttpRequest"/>.</param>
        /// <param name="httpVersion">Version of HTTP protocol that the client uses.</param>
        /// <param name="connectionType">Type of HTTP connection used.</param>
        internal HttpResponse(IHttpClientContext context, string httpVersion, ConnectionType connectionType)
        {
            Check.NotEmpty(httpVersion, "httpVersion");

            Status = HttpStatusCode.OK;
            _context = context;
            _httpVersion = httpVersion;
            m_Connetion = connectionType;
        }
        private ConnectionType m_Connetion;
        public ConnectionType Connection
        {
            get { return m_Connetion; }
            set { return; }
        }

        internal bool ContentTypeChangedByCode
        {
            get { return _contentTypeChangedByCode; }
            set { _contentTypeChangedByCode = value; }
        }

        #region IHttpResponse Members

        /// <summary>
        /// The body stream is used to cache the body contents
        /// before sending everything to the client. It's the simplest
        /// way to serve documents.
        /// </summary>
        public Stream Body
        {
            get { return _body; }
            set { _body = value; }
        }

        /// <summary>
        /// The chunked encoding modifies the body of a message in order to
        /// transfer it as a series of chunks, each with its own size indicator,
        /// followed by an OPTIONAL trailer containing entity-header fields. This
        /// allows dynamically produced content to be transferred along with the
        /// information necessary for the recipient to verify that it has
        /// received the full message.
        /// </summary>
        public bool Chunked { get; set; }


        /// <summary>
        /// Defines the version of the HTTP Response for applications where it's required
        /// for this to be forced.
        /// </summary>
        public string ProtocolVersion
        {
            get { return _httpVersion; }
            set { _httpVersion = value; }
        }

        /// <summary>
        /// Encoding to use when sending stuff to the client.
        /// </summary>
        /// <remarks>Default is UTF8</remarks>
        public Encoding Encoding
        {
            get { return _encoding; }
            set { _encoding = value; }
        }


        /// <summary>
        /// Number of seconds to keep connection alive
        /// </summary>
        /// <remarks>Only used if Connection property is set to <see cref="ConnectionType.KeepAlive"/>.</remarks>
        public int KeepAlive
        {
            get { return _keepAlive; }
            set
            {
                if (value > 400)
                    _keepAlive = 400;
                else if (value <= 0)
                    _keepAlive = 0;
                else
                    _keepAlive = value;
            }
        }

        /// <summary>
        /// Status code that is sent to the client.
        /// </summary>
        /// <remarks>Default is <see cref="HttpStatusCode.OK"/></remarks>
        public HttpStatusCode Status { get; set; }

        /// <summary>
        /// Information about why a specific status code was used.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Size of the body. MUST be specified before sending the header,
        /// unless property Chunked is set to true.
        /// </summary>
        public long ContentLength
        {
            get { return _contentLength; }
            set { _contentLength = value; }
        }

        /// <summary>
        /// Kind of content in the body
        /// </summary>
        /// <remarks>Default type is "text/html"</remarks>
        public string ContentType
        {
            get { return _contentType; }
            set
            {
                _contentType = value;
                _contentTypeChangedByCode = true;
            }
        }

        /// <summary>
        /// Headers have been sent to the client-
        /// </summary>
        /// <remarks>You can not send any additional headers if they have already been sent.</remarks>
        public bool HeadersSent { get; private set; }

        /// <summary>
        /// The whole response have been sent.
        /// </summary>
        public bool Sent { get; private set; }

        /// <summary>
        /// Cookies that should be created/changed.
        /// </summary>
        public ResponseCookies Cookies
        {
            get { return _cookies; }
        }

        /// <summary>
        /// Add another header to the document.
        /// </summary>
        /// <param name="name">Name of the header, case sensitive, use lower cases.</param>
        /// <param name="value">Header values can span over multiple lines as long as each line starts with a white space. New line chars should be \r\n</param>
        /// <exception cref="InvalidOperationException">If headers already been sent.</exception>
        /// <exception cref="ArgumentException">If value conditions have not been met.</exception>
        /// <remarks>Adding any header will override the default ones and those specified by properties.</remarks>
        public void AddHeader(string name, string value)
        {
            if (HeadersSent)
                throw new InvalidOperationException("Headers have already been sent.");

            for (int i = 1; i < value.Length; ++i)
            {
                if (value[i] == '\r' && !char.IsWhiteSpace(value[i - 1]))
                    throw new ArgumentException("New line in value do not start with a white space.");
                if (value[i] == '\n' && value[i - 1] != '\r')
                    throw new ArgumentException("Invalid new line sequence, should be \\r\\n (crlf).");
            }

            _headers[name] = value;
        }

        /// <summary>
        /// Send headers and body to the browser.
        /// </summary>
        /// <exception cref="InvalidOperationException">If content have already been sent.</exception>
        public void Send()
        {
            if (Sent)
                throw new InvalidOperationException("Everything have already been sent.");

            _context.ReqResponseAboutToSend(requestID);
            if (_context.MAXRequests == 0 || _keepAlive == 0)
            {
                Connection = ConnectionType.Close;
                _context.TimeoutKeepAlive = 0;
            }
            else
            {
                if (_keepAlive > 0)
                    _context.TimeoutKeepAlive = _keepAlive * 1000;
            }

            if (!HeadersSent)
            {
                if (!SendHeaders())
                {
                    Body.Dispose();
                    Sent = true;
                    return;
                }
            }

            if(RawBuffer != null)
            {
                if(RawBufferStart >= 0 && RawBufferLen > 0)
                {
                    if (RawBufferStart > RawBuffer.Length)
                        RawBufferStart = 0;

                    if (RawBufferLen + RawBufferStart > RawBuffer.Length)
                        RawBufferLen = RawBuffer.Length - RawBufferStart;

                    int curlen;
                    while(RawBufferLen > 0)
                    {
                        curlen = RawBufferLen;
                        if(curlen > 8192)
                            curlen = 8192;
                        if (!_context.Send(RawBuffer, RawBufferStart, curlen))
                        {
                            RawBuffer = null;
                            RawBufferStart = -1;
                            RawBufferLen = -1;
                            Body.Dispose();
                            return;
                        }
                        RawBufferLen -= curlen;
                        RawBufferStart += curlen;
                    }
                }

                RawBuffer = null;
                RawBufferStart = -1;
                RawBufferLen = -1;
            }

            var buffer = new byte[8192];

            if (Body.Length == 0)
            {
                Body.Dispose();
                Sent = true;
                _context.ReqResponseSent(requestID, Connection);
                return;
            }

            Body.Flush();
            Body.Seek(0, SeekOrigin.Begin);
            int bytesRead = Body.Read(buffer, 0, 8192);
            while (bytesRead > 0)
            {
                if (!_context.Send(buffer, 0, bytesRead))
                {
                    Body.Dispose();
                    return;
                }
                bytesRead = Body.Read(buffer, 0, 8192);
            }


            Body.Dispose();
            Sent = true;
            _context.ReqResponseSent(requestID, Connection);
        }

        /// <summary>
        /// Make sure that you have specified <see cref="ContentLength"/> and sent the headers first.
        /// </summary>
        /// <param name="buffer"></param>
        /// <exception cref="InvalidOperationException">If headers have not been sent.</exception>
        /// <see cref="SendHeaders"/>
        /// <param name="offset">offset of first byte to send</param>
        /// <param name="count">number of bytes to send.</param>
        /// <seealso cref="Send"/>
        /// <seealso cref="SendHeaders"/>
        /// <remarks>This method can be used if you want to send body contents without caching them first. This
        /// is recommended for larger files to keep the memory usage low.</remarks>
        public bool SendBody(byte[] buffer, int offset, int count)
        {
            if (!HeadersSent)
                throw new InvalidOperationException("Send headers, and remember to specify ContentLength first.");

            bool sent = _context.Send(buffer, offset, count);
            Sent = true;
            if (sent)
                _context.ReqResponseSent(requestID, Connection);
            return sent;
        }

        /// <summary>
        /// Make sure that you have specified <see cref="ContentLength"/> and sent the headers first.
        /// </summary>
        /// <param name="buffer"></param>
        /// <exception cref="InvalidOperationException">If headers have not been sent.</exception>
        /// <see cref="SendHeaders"/>
        /// <seealso cref="Send"/>
        /// <seealso cref="SendHeaders"/>
        /// <remarks>This method can be used if you want to send body contents without caching them first. This
        /// is recommended for larger files to keep the memory usage low.</remarks>
        public bool SendBody(byte[] buffer)
        {
            if (!HeadersSent)
                throw new InvalidOperationException("Send headers, and remember to specify ContentLength first.");

            bool sent = _context.Send(buffer);
            if (sent)
                _context.ReqResponseSent(requestID, Connection);
            Sent = true;
            return sent;
        }

        /// <summary>
        /// Send headers to the client.
        /// </summary>
        /// <exception cref="InvalidOperationException">If headers already been sent.</exception>
        /// <seealso cref="AddHeader"/>
        /// <seealso cref="Send"/>
        /// <seealso cref="SendBody(byte[])"/>
        public bool SendHeaders()
        {
            if (HeadersSent)
                throw new InvalidOperationException("Header have already been sent.");

            HeadersSent = true;

            if (_headers["Date"] == null)
                _headers["Date"] = DateTime.Now.ToString("r");
            if (_headers["Content-Length"] == null)
                _headers["Content-Length"] = _contentLength == 0 ? Body.Length.ToString() : _contentLength.ToString();
            if (_headers["Content-Type"] == null)
                _headers["Content-Type"] = ContentType;
            if (_headers["Server"] == null)
                _headers["Server"] = "Tiny WebServer";

            int keepaliveS = _context.TimeoutKeepAlive / 1000;
            if (Connection == ConnectionType.KeepAlive && keepaliveS > 0 && _context.MAXRequests > 0)
            {
                _headers["Keep-Alive"] = "timeout=" + keepaliveS + ", max=" + _context.MAXRequests;
                _headers["Connection"] = "Keep-Alive";
            }
            else
                _headers["Connection"] = "close";

            if (Body.Length != 0)
            {
                if (_headers["Content-Type"] == null)
                    _headers["Content-Type"] = _contentType ?? DefaultContentType;
            }

            var sb = new StringBuilder();
            sb.AppendFormat("{0} {1} {2}\r\n", _httpVersion, (int)Status,
                            string.IsNullOrEmpty(Reason) ? Status.ToString() : Reason);

            for (int i = 0; i < _headers.Count; ++i)
            {
                string headerName = _headers.AllKeys[i];
                string[] values = _headers.GetValues(i);
                if (values == null) continue;
                foreach (string value in values)
                    sb.AppendFormat("{0}: {1}\r\n", headerName, value);
            }

            foreach (ResponseCookie cookie in Cookies)
                sb.AppendFormat("Set-Cookie: {0}\r\n", cookie);

            sb.Append("\r\n");

            _headers.Clear();

            return _context.Send(Encoding.GetBytes(sb.ToString()));
        }

        /// <summary>
        /// Redirect client to somewhere else using the 302 status code.
        /// </summary>
        /// <param name="uri">Destination of the redirect</param>
        /// <exception cref="InvalidOperationException">If headers already been sent.</exception>
        /// <remarks>You can not do anything more with the request when a redirect have been done. This should be your last
        /// action.</remarks>
        public void Redirect(Uri uri)
        {
            Status = HttpStatusCode.Redirect;
            _headers["location"] = uri.ToString();
        }

        /// <summary>
        /// redirect to somewhere
        /// </summary>
        /// <param name="url">where the redirect should go</param>
        /// <remarks>
        /// No body are allowed when doing redirects.
        /// </remarks>
        public void Redirect(string url)
        {
            Status = HttpStatusCode.Redirect;
            _headers["location"] = url;
        }

        #endregion

        /*
         * HTTP/1.1 200 OK
Date: Sun, 16 Mar 2008 08:01:36 GMT
Server: Apache/2.2.6 (Win32) PHP/5.2.4
Content-Length: 685
Connection: close
Content-Type: text/html;charset=UTF-8*/
    }
}