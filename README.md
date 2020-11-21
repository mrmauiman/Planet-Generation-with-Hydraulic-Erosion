


# READ ME
This program was created by Maui Kelley for his senior research at Roanoke College.

## How To Use
When in the PlanetGeneration Scene you can select the Planet object in the Hierarchy tab.  This will bring up a few options in the Inspector tab in the Planet (Script) component.  The options are as followed:

**Generation Variables**
 - *Planet Radius*: controls the radius of the base sphere
 - *Ocean Level*: controls the radius of the ocean sphere
 - *Altitude*: controls the altitude of the highest peak
 - *Icosphere Splits*: controls how many edges each edge of the original Icosahedron should become (The higher the number the more detailed the sphere)
 - *GPC Latitude Chunks and GPC Longitude Chunks*: controls how many chunks are created to put vertices in on based on their global positioning coordinates

**Perlin Noise Variables**
 - *Offset*: this is essentially the seed of the perlin noise generation
 - *Cell Sizes*: this is the cell sizes of the four levels of perlin noise (The smaller the number the grainer the noise)
 - *Weights*: this is how much of an effect each level of noise should have on the final output

**Simulation Variables**
 - *Simulate Weather*: whether or not the weather simulation should be used
 - *Total Simulation Iterations*: How long should the simulation run
 - *Simulation Chunk Sizes*: How many iterations should be done before updateing the mesh

**Weather Variables**
 - *Show Clouds*: controls whether or not you can visually see the clouds
 - *Cloud Spawn Chance*: the likely hood of a cloud spawning every iteration
 - *Cloud Model Scale*: the visual size of the clouds
 - *Cloud Size Range*: the range of actual sizes of a cloud (clouds are sized in GPC space so this)
 - *Cloud Life Span Range*: the range of how long a cloud will exist for (you can think of this as how much water a cloud can have)
 - *Cloud Prefab*: the prefab of the cloud visuals

**Erosion Variables**
 - *Weatherless Droplet Iterations*: the number of droplets to spawn in a single iteration when wheather is turned off
 - *Erosion Radius*: the radius around a droplet to erode
 - *Inertia*: the willingness of a droplet to change directions (0: the droplet will never change directions | 1: the droplet will instantly change directions)
 - *Sediment Capacity Factor*: the base amount of sediment a droplet can carry
 - *Min Sediment Capacity*: the minimum amount of sediment a droplet must have (must be more than 0)
 - *Erode Speed*: how much of a droplets capacity it will erode in an iteration
 - *Deposit Speed*: How much of the capacity to deposit in an iteration
 - *Evaporate Speed*: How fast a droplet loses water
 - *Gravity*: the effect of gravity on a droplets speed
 - *Max Droplet Lifetime*: the distance a droplet will go before being deleted
 - *Initial Water Volume*: the amount of water a droplet starts with
 - *Initial Speed*: the initial movement speed of a droplet

**Material Options**
There are also options under the Planet Material (Material) component.

 - *Shore Level*: the radius to color sand up to
 - *Cliff Threshold*: the amount of a slope to color as grass or rock (The higher the number the more that is colored as grass)
 - *Sand Color*: the color of sand
 - *Grass Color*: the color of grass
 - *Rock Color*: the color of rock

**Running the Simulation**
In the top right of the Planet (Script) component there is a hamburger button (three vertical dots)  that you can press.  At the bottom of that context menu is three options:

 - *Generate*: Press this to generate a new model
 - *Run Simulation*: Press this to run the simulation
 - *Show GP Space*: Press this to form the mesh into it's global positioning space representation. You must regenerate the mesh to undo this.
