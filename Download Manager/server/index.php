<?php
	extract($_GET);
	
	//get the length from the url
	//and print the length
	$length = get_headers($url, 1)['Content-Length'];
	if(gettype($length) == "array") {
		$length = end($length);
	}	
	print($length);
	
	//mysql parameters
	$server = "localhost";
	$user = "u466721340_user";
	$password = "123456";
	$database = "u466721340_data";
	$query = "insert into url(url, size) values('" . $url . "'," . $length . ");";
	
	//sql connection and query
	$conn = new mysqli($server, $user, $password, $database) or die();
	$conn->query($query);
?>