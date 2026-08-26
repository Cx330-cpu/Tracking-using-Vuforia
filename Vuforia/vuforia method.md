# Evaluation of Vuforia Model Target Based Object Tracking for AR Object Replacement

## 1. Method Overview

This section evaluates the feasibility of using **Vuforia Engine Model Target** for AR object replacement.

The Vuforia Model Target approach uses a predefined 3D model of the target object to achieve object recognition and 6DoF pose estimation.

Unlike traditional image targets, which rely on 2D visual features, Model Target uses the geometric structure of the object. A CAD/3D model is first imported into **Vuforia Model Target Generator (MTG)** to generate recognition data. During runtime, the camera input is compared with the generated model representation to estimate the object's position and orientation.

The overall workflow is:

```
Real object
      ↓
Camera input
      ↓
Vuforia Model Target recognition
      ↓
6DoF pose estimation
      ↓
Unity AR tracking
      ↓
Virtual object replacement
```

### Implementation Environment

- Unity 2022.3 LTS
- Vuforia Engine
- Vuforia Model Target Generator
- iPhone 16 Pro Max as target object

# 2. Experimental Setup

## 2.1 Target Object

The tested object is an **iPhone 16 Pro Max**.

A 3D model of the smartphone was imported into Vuforia Model Target Generator and integrated into Unity.

The evaluation focuses on:

1. Tracking under normal visibility conditions
2. Different surface characteristics
3. Partial hand occlusion
4. Different Vuforia tracking modes


## 2.2 Evaluation Conditions

The following conditions were tested:

| Condition | Purpose |
|---|---|
| Rear surface visible | Evaluate optimal tracking performance |
| Rear surface partially occluded | Evaluate robustness against hand blocking |
| Rear surface severely occluded | Identify tracking failure boundary |
| Front glass screen | Evaluate reflective surface limitation |
| Different Vuforia tracking modes | Compare available solutions |

# 3. Experimental Results

# 3.1 Rear Surface Tracking (Best Performance)

## Experimental Result

The rear side of the iPhone provides the most stable tracking performance.

The reason is that the rear surface contains distinctive geometric structures:

- Camera module protrusion
- Depth variations
- Non-flat surface features

These structures provide stronger geometric constraints for Model Target matching.


## Figure 1. Successful rear-side tracking under normal condition

![Successful Tracking condition](image/1.png)
![Successful Tracking condition](image/2.png)
![Successful Tracking condition](image/3.png)
## Observation

Under normal visibility conditions:

- Recognition is fast
- Tracking remains stable
- 6DoF pose estimation is accurate
- Virtual object alignment shows low drift

The rear camera module is an important feature because it provides additional geometric information compared with a completely flat surface.

# 3.2 Hand-held Tracking and Occlusion Evaluation

## Case 1: Severe Hand Occlusion

When more than approximately 50% of the rear surface is covered by fingers, tracking fails.

## Figure 2. Tracking failure under severe hand occlusion

![Failure Tracking condition](image/4.png)

## Observation

The system shows:

- Loss of tracking state
- Unstable pose estimation
- Failure to maintain virtual object alignment

The main reason is that Vuforia Model Target depends on visible geometric information.

When important geometric structures are blocked, especially the camera module area, insufficient information remains for reliable 6DoF estimation.

# Case 2: Occlusion Boundary Condition

A boundary condition was tested where partial hand occlusion was present but tracking was still possible.

## Figure 3. Tracking boundary condition with partial occlusion

![Failure Tracking condition](image/5.png)


## Observation

Tracking remains successful when:

- The camera module is still visible
- Enough rear geometry remains observable
- Feature distribution is sufficient

This indicates that tracking robustness is not only determined by the percentage of occlusion, but also by the location of the missing information.

Blocking important geometric regions has a stronger impact than blocking flat areas.

# 3.3 Front Surface Tracking Evaluation

## Original Front Screen

The front side of the iPhone was tested.

The result shows that the original front screen is unsuitable for reliable Model Target tracking.

## Figure 4. Tracking failure on reflective front screen

![Failure Tracking condition](image/6.png)

## Observation

The main limitations are:

- Smooth glass surface
- Strong reflection
- Lack of geometric features
- Appearance changes under different lighting conditions

Although the physical shape of the phone remains unchanged, the observed camera image contains insufficient stable information for Model Target matching.


# 3.4 Artificial Feature Enhancement Experiment

To investigate whether additional visual information could improve tracking, a specific image was displayed on the phone screen and included during Model Target generation.

After learning this additional appearance information through MTG, the front surface became trackable.

## Observation

Although tracking performance improved, this approach changes the original condition:

- The system relies on artificial screen content
- Tracking depends on the displayed image remaining unchanged
- It does not represent normal smartphone usage

Therefore, this method improves recognition but reduces general applicability.


# 4. Evaluation of Different Vuforia Tracking Modes

Three existing Vuforia configurations were considered:

- Default
- Low Feature Objects
- AR Controller



# 4.1 Default Mode

Default mode was selected for the current experiments.

## Advantages

- General-purpose Model Target tracking
- Suitable for objects with distinctive geometry
- Provides a reliable baseline evaluation

For the iPhone case:

- Rear camera module provides strong geometric features
- Stable tracking can be achieved under sufficient visibility

## Limitation

- Sensitive to severe occlusion
- Performance decreases when important geometry is unavailable


# 4.2 AR Controller Mode

AR Controller mode was considered because it is designed for moving and hand-held objects.

It is more suitable for objects with:

- Rich surface textures
- Clear visual patterns
- Non-reflective surfaces

Examples:

- Controllers
- Toys
- Printed objects

However, the iPhone is not an ideal candidate because:

- Metal/glass surface
- Smooth appearance
- Limited texture information
- Strong reflection

Therefore, although AR Controller supports moving objects, it does not effectively solve the smartphone tracking problem.

# 4.3 Low Feature Objects Mode

Low Feature Objects mode was considered because smartphones contain limited surface features.

It is designed for:

- Smooth objects
- Low-texture objects
- Controlled environments

However, it is mainly optimized for controlled/static scenarios and does not directly address dynamic hand-held tracking.

Therefore, it does not fully satisfy the requirement of real-time AR object replacement with moving objects.

# 5. Overall Comparison

| Condition | Result | Explanation |
|---|---|---|
| Rear surface visible | Excellent tracking | Camera module provides strong geometry |
| Rear surface partially occluded | Performance decreases | Reduced visible geometry |
| Rear surface >50% covered | Tracking failure | Insufficient geometric constraints |
| Front glass screen | Tracking failure | Reflection and lack of features |
| Front screen with learned image | Improved | Artificial features introduced |

# 6. Strengths and Limitations

## Strengths

- Mature commercial AR tracking solution
- Accurate 6DoF pose estimation
- Easy Unity integration
- No requirement for custom machine learning training


## Limitations

- Requires predefined 3D model
- Sensitive to severe occlusion
- Sensitive to reflective and textureless surfaces
- Difficult for hand-held objects with frequent blocking

# 7. Existing Work and Self-developed Contribution

The Vuforia Model Target technology and related tracking modes are existing features provided by Vuforia Engine.

They are not self-developed algorithms.

The contribution of this work is:

- Integrating Vuforia Model Target into an AR object replacement pipeline
- Evaluating its performance under realistic conditions
- Analysing limitations including:

  - Hand occlusion
  - Reflective surfaces
  - Different object orientations
  - Tracking mode suitability

# 8. Discussion

The experiments demonstrate that Vuforia Model Target provides accurate and stable tracking when sufficient geometric information is available.

However, real-world AR object replacement introduces additional challenges.

For smartphone-like objects:

- The rear camera module improves tracking reliability.
- Hand occlusion significantly reduces available geometric information.
- Reflective glass surfaces provide insufficient stable features.

Therefore, although Vuforia provides a practical solution for controlled AR replacement scenarios, additional approaches may be required for robust tracking of hand-held objects under arbitrary conditions.

# 9. Future Improvement Directions

Possible improvement directions include:

1. Combining Model Target tracking with additional RGB/depth information.

2. Using Vuforia Device Tracking and Extended Tracking to improve recovery after partial tracking loss.

3. Investigating advanced object pose estimation methods, such as RGB-D based neural pose estimation approaches.

4. Exploring methods specifically designed for hand-occluded object tracking.

# 10. References

1. Vuforia Engine Documentation  
https://developer.vuforia.com/

2. Vuforia Model Target Documentation  
https://developer.vuforia.com/library/

3. Vuforia Model Target Generator Documentation  
https://developer.vuforia.com/library/applications/model-target-generator/
