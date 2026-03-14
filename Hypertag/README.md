The hypertag system allows for users to identify and find objects in a scene, without having to have references.
It kind of extends the normal Unity tag system, but allows for multiple tags on a single object, and since Hypertags are modular scriptable objects, we can add modules to the tags themselves.

# TODO

* A hypertag that has a parent should count as being an hypertag of the type of the parent
  * Need to change a lot of code to make this happen, and test all the find functions, etc, a lot of work.