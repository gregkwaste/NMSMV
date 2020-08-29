#Custom build script for the NMSMV

from zipfile import ZipFile
import sys
import os

def info():
    pass

def pack(configName, versionText):
    filepath = os.path.join("../bin/", configName)

    usefulExtensions = ['dll', 'exe']

    # writing files to a zipfile
    with ZipFile('NMSMV_v' + versionText + '.zip', 'w') as zip:
        for filename in os.listdir(filepath):
            if (filename.split('.')[-1] in usefulExtensions):
                zip.write(os.path.join(filepath, filename))
    
        #write shaders
        shaderPath = os.path.join(filepath, "Shaders")
        for filename in os.listdir(shaderPath):
            if (filename.split('.')[-1] == 'glsl'):
                zip.write(os.path.join(shaderPath, filename))


if __name__ == "__main__":
    print(sys.argv)

    #DEBUG
    sys.argv.append("Release")
    sys.argv.append("0.90.0")

    #Get configuration name
    try:
        configName = sys.argv[1]
        version = sys.argv[2]
    except:
        print("Problem with input arguments")
        assert(False)


    print("Packing Configuration:", configName, "Version:", version)
    
    pack(configName, version)









