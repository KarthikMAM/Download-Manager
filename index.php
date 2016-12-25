<?php
	extract($_GET);

	//get the length from the url
	//and print the length
	$length = get_headers($url, 1)['Content-Length'];
	if(gettype($length) == "array") {
		$length = end($length);
	}
	print($length);
	syslog(LOG_INFO, $url . $length);
?>
