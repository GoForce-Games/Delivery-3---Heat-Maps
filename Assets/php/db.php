<?php
$host = "localhost";
$user = "edgarmd1";
$password = "Afkz4n4Msd7z";
$database = "edgarmd1";

$conn = new mysqli($host, $user, $password, $database);

if ($conn->connect_error) {
    die("DB Connection failed");
}
?>