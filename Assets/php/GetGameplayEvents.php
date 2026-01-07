<?php
include("db.php");

$eventType = $_POST["eventType"];
$sessionID = $_POST["sessionID"];

$queries = array();

switch ($eventType) {
    case "Muerte":
        $queries[] = "SELECT session_id, 'muerte' AS eventType, pos_x, pos_y, 0 AS pos_z, timestampo FROM death_event";
        break;

    case "Golpe":
        $queries[] = "SELECT session_id, 'golpe' AS eventType, pos_x, pos_y, 0 AS pos_z, timestampo FROM hit_event";
        $queries[] = "SELECT session_id, 'golpe' AS eventType, pos_x, pos_y, 0 AS pos_z, timestampo FROM damage_event";
        break;

    case "Salto":
        $queries[] = "SELECT session_id, 'salto' AS eventType, pos_x, pos_y, pos_z, timestampo FROM jump_event";
        break;

    case "Enemigo":
        $queries[] = "SELECT session_id, 'enemigo' AS eventType, pos_x, pos_y, pos_z, timestampo FROM kill_event";
        break;

    case "Ruta":
    default:
        $queries[] = "SELECT session_id, 'posicion' AS eventType, pos_x, pos_y, pos_z, timestampo FROM run_event";
        $queries[] = "SELECT session_id, 'posicion' AS eventType, pos_x, pos_y, pos_z, timestampo FROM walk_event";
        break;
}

$events = array();

foreach ($queries as $q) {
    if ($sessionID != "Ver Todo") {
        $q .= " WHERE session_id = '$sessionID'";
    }

    $result = mysqli_query($conn, $q);

    while ($row = mysqli_fetch_assoc($result)) {
        $events[] = array(
            "sessionID" => $row["session_id"],
            "eventType" => $row["eventType"],
            "position" => array(
                "x" => floatval($row["pos_x"]),
                "y" => floatval($row["pos_y"]),
                "z" => floatval($row["pos_z"])
            ),
            "timestamp" => $row["timestampo"],
            "sessionDuration" => 0
        );
    }
}

echo json_encode(array("events" => $events));
?>