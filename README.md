# First person player controller
### for a game I wanted to make in godot

#### todo / done

    [x] walking

    [x] crouching

    [x] jumping

    [x] sprinting

    [ ] climbing?

    [ ] fall damage

    [ ] rigidbody interaction and damage

    [ ] camera bobbing

    [ ] slide when crouching after sprint

#### showcased in this demo, and some notes

    slopes
        - default: above 50 degrees slope is considered "not floor"
        - change player shape in editor to adjust this threshold

    stairs
        - default: 0.5 units tall obstacle is considered "stair"
        - change player shape in editor to adjust maximum step size
        - note: might change in the future to support rigidbodies
