# TheVersionator
Idempotently returns the next semantic version number of a docker image by inspecting the registry where the image will ultimately be pushed.  Depends on Registry v2 API and uses Basic auth.  What TheVersionator actually does is fetches all the tags from the registry, sorts them and finds the highest semantic version (with a matching suffix) and prints out the very next version number.  If you tell it to bump the minor or major number, it does what you expect.

## Why, just why?
I love git, but looking at hashes for a versioning mechanism is not terribly helpful.  Semantic versioning is just easier to look at and spot problems, but a simple system that works with branches is something everyone needs and often rolls their own (badly).  Often it means checking in files into a repository by the build machine (gross).  Sometimes it means setting up a whole DB just for the build machine to ask what version number to use (overkill).  Even worse is just writing to a text file and not checking it in anywhere on the local build machine, which is asking for trouble in so many ways.  At the end of the day, we all have other things to worry about, though.  

The right answer, usually, is to use git tags.  It's a good way to shove metadata into the repo without causing a commit.  In my case, the team I'm on is not using git, but I want that same kind of functionality.  It occurred to me that all the versioning information exists in the private docker container registry that we run, as a set of versioned tags on each image.  TheVersionator produces the correct next version number quickly, efficiently, and doesn't need to store it anywhere, because the output of a successful build is the image that gets pushed... which bumps the version number for the next build!  Voila!

Clearly a race condition exists when trying to do multiple builds of the same branch/suffix simultaneously before the first build completes.  I'm not attempting to solve that.  If you're really motivated, you could quickly push a tagged 1-byte image just to bump the version number, but it's not a problem I care about.

## Building
If you grab the project and open the .sln, hit Build -> Publish and it will compile to a self-contained .exe in the bin\Release\net5.0\publish folder.  Throw that into your path and you now have a way to generate the next version tag for your docker image based on whatever tags are already pushed to your Docker Container Registry v2.
```
TheVersionator 0.0.1
Project Page: https://github.com/jhughes2112/TheVersionator

  -r, --registry    Required. Full url to the docker registry, such as https://some.registry.com/

  -c, --config      Required. Path to a text file that contains exactly two lines in it.  Username on the first, password on the second.

  -i, --image       Required. Image that will be used to request for version tags.

  -s, --suffix      If specified, tags are only considered if the suffix matches.  If not specified, tags with suffixes are ignored.
                    (minor.major.patch-suffix)

  --minor           Bumps the minor revision. (major.minor.patch-suffix)

  --major           Bumps the major revision. (major.minor.patch-suffix)

  --help            Display this help screen.

  --version         Display version information.
```
  
## Usage
What TheVersionator really does is reads all the existing tags and figures out what the highest semver is, then bumps it.  Here's what the registry actually contains:
```
D:\> curl -u "jhughes:mypassword" https://some.registry.com/v2/edge/tags/list
{"name":"myimage","tags":["1.0.0","latest"]}

D:\> TheVersionator.exe -i myimage -c userpass.txt -r https://some.registry.com/
1.0.1

D:\> TheVersionator.exe -i myimage -c userpass.txt -r https://some.registry.com/ --minor
1.1.0

D:\> TheVersionator.exe -i myimage -c userpass.txt -r https://some.registry.com/ --major
2.0.0

D:\> TheVersionator.exe -i myimage -c userpass.txt -r https://some.registry.com/ --suffix jason
0.0.1-jason
```
