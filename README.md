# No Mans's Model Viewer #
![char.png](http://i.imgur.com/KkotCjBl.png)
No Man's Model Viewer is a model viewer created to preview No Man's Sky 3D Models. It also supports animation playback and also a custom Procedural Generation which tries to emulate the game's behavior during the creation process.

### Repo Version ###

* Latest Version 0.4

### How do I get set up? ###

* Summary of set up
All you need to do is to build the project's solution file.
* Configuration
None
* Dependencies
OpenTK is the one and only dependency for building the project. Also make sure to install the latest NET framework updates and you may also need to update your graphics card drivers.

### Contribution guidelines ###
* Please Report any Issues or any suggestions you may have.

### Who do I talk to? ###

* Send me an email at gregkwaste@gmail.com

##USAGE##
* First of all make sure that you have unpacked all of the game files. The *.SCENE.MBIN files may reference many different files which are needed to load the scene properly. 
* After loading a scene file you can also load the appropriate animations for the specific models. The animations are located in the ANIMS folder which is located in the same folder with the SCENE file.
![procgen.png](http://i.imgur.com/G5MqNfHl.png)
* On procgen models (which is the majority of the models in the game) there is also an option to create a whole generation of those types of models like the game would do, using a very similar procedural generation algorithm.