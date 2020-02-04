# TimeScaler

TimeScaler is a component that enables some per-object control of timescale in Unity.
For kinematic objects, programmers will have to support this by fetching the component and adapting their code to account for the modified timescale (easy enough, as you can see in the example below of a bullet-like component):

    void Start()
    {
        timeScaler = GetComponent<TimeScaler2d>();

        timeScaler.originalVelocity = transform.right * speed;
    }

The originalVelocity attribute just sets the velocity of the underlying Rigidbody2d to the given velocity scaled by the timescale in the TimeScaler component. If you fetch it, you will get the "original velocity", instead of the modified one, to minimize the need to change the code.
Another example (that doesn't use a Rigidbody2d component):

    void Update()
    {
        transform.position += velocity * timeScaler.timeScale;
    }

The TimeScaler component is slightly more complex if we're using a dynamic Rigidbody2d.
In that case, the TimeScaler does a lot more to try to modify the Rigidbody2d behavior to account for any timescaling.
Every time the timescale is changed, the velocity, angularVelocity, gravityScale, drag, angularDrag and mass of the object is modified to account for the change.
The results are convincing, yet not perfect at all... For example, let's imagine we have a room and a suspended object (with a timescaler component) in the middle of it. If we drop the object, it will fall in some time T and with a velocity of V when it hits the ground.
Now, if we add a "time warp" zone in the middle of the room, that sets the timescale of the object to 0.5 on entry and to 1.0 on exit of the collider, the object will take longer to drop to the ground (as expected), and we'll see it slowly traversing the zone, but it will hit the floor with a larger velocity than V.
This is due to the fact that the object has more air time to gain velocity. A fix for this (I believe) would be to add some linear drag to the object, to make it have a terminal velocity, and then that drag can be scaled by the warping zone in some way to account for the fact that objects can't speed up indefinitely...

Still, the results are quite convincing for simple games!

## Credits

* Code by [Diogo Andrade]

## Licenses

All code in this repo is made available through the [GPLv3] license.

## Metadata

* Autor: [Diogo Andrade][]

[Diogo Andrade]:https://github.com/DiogoDeAndrade
[GPLv3]:https://www.gnu.org/licenses/gpl-3.0.en.html
[CC-BY-SA 3.0.]:http://creativecommons.org/licenses/by-sa/3.0/