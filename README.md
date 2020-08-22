# **No Mans's Model Viewer** #

<div align="center"> <img src="https://i.imgur.com/hdBRZFL.png" width="400px"> </div>
<p></p>


<div align="center">
<img alt="GitHub tag (latest by date)" src="https://img.shields.io/github/v/tag/gregkwaste/NMSMV">
<img alt="GitHub release (latest by date)" src="https://img.shields.io/github/v/release/gregkwaste/NMSMV">
<a href="https://github.com/gregkwaste/NMSMV/issues"><img alt="GitHub issues" src="https://img.shields.io/github/issues/gregkwaste/NMSMV"></a>
</div>


No Man's Model Viewer is an application developed to preview No Man's Sky 3D assets. It also supports animation playback and also a custom proc-gen procedure which tries to emulate the game's behavior during the procedural asset creation process. Experimental features allow the live editing of MBIN files. 

## **Repo Version** ##

* [Latest Version 0.89.4](https://github.com/gregkwaste/NMSMV/releases)
* [Wiki Page - OUTDATED](https://github.com/gregkwaste/NMSMV/wiki)

## **Features** ##
* Preview of .SCENE.MBIN files
* Support for Diffuse/Normal/Mask (roughness/metallic/ao) maps
* Support for animation playback on both skinned and static models, via parsing of the corresponding entity files
* Optimized renderer to support the rendering of very large scenes and increased framerates (well as much as .NET allows...)
* Basic implementation of a PBR shader pipeline that tries to emulate game shaders in an effort to preview assets as close to the game as possible (so close yet so far...)
* Procedural texture generation that tries to emulate NMS's texture missing process of procedural assets
* Procedural model generation (broken for a while, repair pending)
* Basic scenegraph editing (scenenode translation/rotation/scaling)
* Interface with libMbin to allow for a much more robust asset import and export directly to MBIN/EXML file format.
* Interface with libPSARC to allow for direct browsing of PAK contents (No need to unpack game files to browse through the models)

## **WIP** ##
* 3D Gizmo implementation for a much more robust node manipulation.
* The project includes 2 more subprojects, a) a texture mixing app that provides a testbench of the procedural texture mixing process of NMS as implemented in the viewer and b) a texture viewer that interfaces with a custom DDS library to allow the preview of DX10 textures as well as multi-textures. I'm planning to better organize and maintain their code and eventually pack them together with the viewer in future releases.
* Assemble some wiki pages to explain the functionality and editing capabilities of the viewer.


### Build from Source? ###

TODO

### Contribution guidelines ###
* Please use the issue tracker to report any issues or any features/suggestions you may have.


## **Screenshots** ##
<div align="center"> <img src="https://i.imgur.com/9NX73V1h.png"></div>

## **Credits** ##
* monkeyman12 main maintainer of [libMBIN](https://github.com/monkeyman192/MBINCompiler)
* Fuzzy-Logik main maintainer of [libPSARC](https://github.com/Fuzzy-Logik/libPSARC)
* IanM32 for the amazing logo

## **Contact** ##
* Send me an email at gregkwaste@gmail.com
