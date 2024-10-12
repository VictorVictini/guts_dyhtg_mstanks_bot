public enum NetworkMessageType
{
    createTank = 1,
    despawnTank = 2,
    fire = 3,
    toggleForward = 4,
    toggleReverse = 5,
    toggleLeft = 6,
    toggleRight = 7,
    toggleTurretLeft = 8,
    toggleTurretRight = 9,
    turnTurretToHeading = 10,
    turnToHeading = 11,
    moveForwardDistance = 12,
    moveBackwardsDistance = 13,
    stopAll = 14,
    stopTurn = 15,
    stopMove = 16,
    stopTurret = 17,
    objectUpdate = 18,
    healthPickup = 19,
    ammoPickup = 20,
    snitchPickup = 21,
    destroyed = 22,
    enteredGoal = 23,
    kill = 24,
    snitchAppeared = 25,
    gameTimeUpdate = 26,
    hitDetected = 27,
    successfulHit =28
}