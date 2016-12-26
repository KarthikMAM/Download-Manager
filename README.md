# Proxy File Size Server

<h4>Why I need this server for my downloader?</h4>
<ul>
    <li>My college proxy server reads the headers for all requests and blocks requests with Content-Length over 50MB
    <li>Inorder to fool it I am using a proxy file size server which returns the Content-Length
    <li>Now, if the request is byte addressed then i can use range header to download the file in parts
    <li>So, technically I am not violating the restriction but cirumventing it.
    <li>Now I can download big files without any restrictions
</ul>


<h4><a href="http://proxyfilesize.appspot.com/index.php?url=http://filehippo.com">Content-Length for the request filehippo.com</a></h4> 