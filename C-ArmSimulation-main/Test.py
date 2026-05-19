import open3d as o3d
import numpy as np
import os
import json
import copy
from yourdfpy import URDF

# ---------------------------------Definitions of Variables---------------------------------
default_resolution = 0.05  # Default resolution for voxel grid
default_room_dimensions = (9.0, 3.0, 5.0)  # Default room dimensions (width, length, height)
default_wall_thickness = 0.01  # Default wall thickness for visualization
DepthImageFolder = "DepthCaptures"  # Folder containing depth images
X_OFFSET = 1.0  # Adjust as needed to align with Open3D's coordinate system
Y_OFFSET = 0.0  # Adjust as needed to align with Open3D's coordinate system
Z_OFFSET = 0.0  # Adjust as needed to align with Open3D's coordinate system
JSON_NAMING_CONVENTION = {
    "Carriage": "Long",
    "HorBeam": "Z1Rot",
    "VerBeam": "Z2Rot",
    "Sleeve": "Prop",
    "CArc": "CArc",
}


# ---------------------------------Class Definitions---------------------------------


class RobotModel:
    def __init__(self, hulls_folder="Robotarm/hulls", urdf_path="Robotarm/FlexArmStudents.urdf"):
        self.hulls_folder = hulls_folder
        self.urdf_path = urdf_path
        self.parts = {}
        self._load_all_hulls()
        self.base_parts = {name: copy.deepcopy(mesh) for name, mesh in self.parts.items()}
        self.urdf_model = URDF.load(self.urdf_path)
        self.urdf_model.update_cfg(self.urdf_model.zero_cfg)
        self.zero_link_transforms = {
            link_name: np.asarray(self.urdf_model.get_transform(link_name), dtype=np.float64)
            for link_name in self.urdf_model.link_map.keys()
        }

    def _load_all_hulls(self):
        for filename in os.listdir(self.hulls_folder):
            if filename.endswith(".stl"):
                name = os.path.splitext(filename)[0]
                path = os.path.join(self.hulls_folder, filename)
                
                # Laden van de mesh
                mesh = o3d.io.read_triangle_mesh(path)
                mesh.compute_vertex_normals()
                mesh.paint_uniform_color([0.6, 0.6, 0.7])
                
                self.parts[name] = mesh
    
    def get_all_meshes(self):
        return list(self.parts.values())

    def transform_part(self, part_name, matrix):
        if part_name in self.parts:
            self.parts[part_name].transform(matrix)

    def transform_entire_robot(self, matrix):
        for name in self.parts:
            self.transform_part(name, matrix)

    def reset_to_base_pose(self):
        self.parts = {name: copy.deepcopy(mesh) for name, mesh in self.base_parts.items()}

    def _parts_for_link(self, link_name):
        return [part_name for part_name in self.parts if part_name.startswith(link_name)]

    def apply_urdf_fk_pose(self, joint_values=None, world_transform=None):
        joint_values = joint_values or {}
        world_transform = np.eye(4) if world_transform is None else np.asarray(world_transform, dtype=np.float64)

        self.urdf_model.update_cfg(joint_values)

        self.reset_to_base_pose()

        for link_name in self.urdf_model.link_map.keys():
            matching_parts = self._parts_for_link(link_name)
            if not matching_parts:
                continue

            link_transform = np.asarray(self.urdf_model.get_transform(link_name), dtype=np.float64)
            delta_transform = link_transform @ np.linalg.inv(self.zero_link_transforms[link_name])
            full_transform = world_transform @ delta_transform
            for part_name in matching_parts:
                self.parts[part_name].transform(full_transform)





class RoomPointCloud():
    def __init__(self):
        self.point_cloud = o3d.geometry.PointCloud()

    def apply_noise(self, depth_data, sigma = 0.1, dropout_pixel=0.01):
        """Applies Gaussian noise and random dropout to the depth data."""
        noisy_depth = depth_data.copy()
        
        # Apply Gaussian noise
        noise = np.random.normal(0, sigma, size=depth_data.shape)
        noisy_depth += noise
        
        # Apply random dropout
        dropout_mask = np.random.rand(*depth_data.shape) < dropout_pixel
        noisy_depth[dropout_mask] = 0  # Set dropped pixels to zero (or some invalid value)
        
        return noisy_depth
    
    def remove_noise_2d(self, depth_data, sigma=0.1):
        """Applies a simple Gaussian filter to reduce noise in the depth data."""
        from scipy.ndimage import median_filter#, gaussian_filter
        mask  = (depth_data > 0)  # Only filter valid depth pixels

        # First apply a median filter to remove salt-and-pepper noise
        filtered = median_filter(depth_data, size=3)
        return np.where(mask, filtered, 0)
    
    def remove_noise_3d(self, pcd, nb_neighbors=20, std_ratio=2.0):
        """Removes noise from the point cloud using statistical outlier removal."""
        cl, ind = pcd.remove_statistical_outlier(nb_neighbors=nb_neighbors, std_ratio=std_ratio)
        return pcd.select_by_index(ind)

    def add_DepthImage(self, image_path, intrinsic, extrinsic=np.eye(4),add_noise=True):
        """Converts depth image to PCD and adds it to the room using camera extrinsics."""
        # Unity .raw files are often binary float32 arrays
        width, height = intrinsic.width, intrinsic.height
        depth_data = np.fromfile(image_path, dtype=np.float32).reshape((height, width))

        # add noise to the depth data to simulate real-world conditions
        if add_noise:
            depth_data = self.apply_noise(depth_data, sigma=0.02, dropout_pixel=0.01)

        
        #depth_data = self.remove_noise_2d(depth_data, sigma=1.0)
    
        # Unity GetPixels order is bottom-to-top; Open3D expects top-to-bottom
        # Add np.flipud here to fix the vertical inversion
        depth_data = np.ascontiguousarray(np.flipud(depth_data))
        depth_o3d = o3d.geometry.Image(depth_data)
        
        # --- COORDINATE SYSTEM CONVERSION ---
        # 1. Fix the Camera: Flip Local Y and Z axes (Rows 1 and 2)
        extrinsic[1, :] *= -1
        extrinsic[2, :] *= -1  
        
        # 2. Fix the World: Flip Global Z axis (Column 2) to stop the "X" crossover
        extrinsic[:, 2] *= -1 
        
        # Force float64 and memory contiguity to prevent Open3D Segfaults
        extrinsic_clean = np.ascontiguousarray(extrinsic, dtype=np.float64)
        
        #print("Intrinsic:", intrinsic)
        #print("Extrinsic:\n", extrinsic_clean)
        
        pcd = o3d.geometry.PointCloud.create_from_depth_image(
            depth_o3d, intrinsic, extrinsic=extrinsic_clean, depth_scale=1.0, depth_trunc=10.0
        )
        self.point_cloud += pcd

    def crop_point_cloud(self, x_bound,y_bound,z_bound,margin=0.5):
        """Crops the point cloud to fit within the defined room dimensions plus a margin."""
        if self.point_cloud.is_empty():
            print("Error: Point cloud is empty. Cannot crop.")
            return
        
        # Define the bounding box for cropping
        min_bound = np.array([x_bound[0] - margin, 
                              y_bound[0] - margin, 
                              z_bound[0] - margin])
        max_bound = np.array([x_bound[1] + margin, 
                              y_bound[1] + margin, 
                              z_bound[1] + margin])
        
        # Crop the point cloud using the defined bounding box
        cropped_pcd = self.point_cloud.crop(o3d.geometry.AxisAlignedBoundingBox(min_bound, max_bound))
        
        # Update the point cloud with the cropped version
        self.point_cloud = cropped_pcd

    def add_point_cloud(self, pcd):
        self.point_cloud += pcd

    def color_point_cloud(self, color):
        """Colors the entire point cloud with a single color."""
        if self.point_cloud.is_empty():
            print("Error: Point cloud is empty. Cannot color.")
            return
        self.point_cloud.paint_uniform_color(color)

    def clear(self):
        self.point_cloud.clear()

    def visualize(self):
        if self.point_cloud.is_empty():
            print("Error: Point cloud is empty. Check if the sample directory contains valid .raw files.")
            return
        self.point_cloud.estimate_normals() # Still need normals for light!
        # Use the modern web/PBR viewer
        o3d.visualization.draw_geometries([self.point_cloud], window_name="Room Visualization (Point Cloud)")

    def filter_robot_points(self, robot_model, make_red=True, extra_margin_m=0.0):
        if self.point_cloud.is_empty() or not robot_model.parts:
            return

 
        scene = o3d.t.geometry.RaycastingScene()
        
        for name, mesh in robot_model.parts.items():
            t_mesh = o3d.t.geometry.TriangleMesh.from_legacy(mesh)
            scene.add_triangles(t_mesh)

        query_points = o3d.core.Tensor(np.asarray(self.point_cloud.points), dtype=o3d.core.Dtype.Float32)
        occupancy = scene.compute_occupancy(query_points)
        is_robot_mask = occupancy.numpy().astype(bool)

        # Expand the robot exclusion zone by a user-defined margin around the hull.
        if extra_margin_m > 0.0:
            signed_distance = scene.compute_signed_distance(query_points).numpy()
            is_robot_mask = np.logical_or(is_robot_mask, signed_distance <= extra_margin_m)

        points_count = len(self.point_cloud.points)
        if make_red:
            colors = np.asarray(self.point_cloud.colors)
            if colors.size == 0:
                colors = np.tile([0.5, 0.5, 0.5], (points_count, 1))
            colors[is_robot_mask] = [1.0, 0.0, 0.0]
            self.point_cloud.colors = o3d.utility.Vector3dVector(colors)
        else:
            self.point_cloud = self.point_cloud.select_by_index(np.where(~is_robot_mask)[0])
        
        print(
            f"Exacte filtering voltooid: {np.sum(is_robot_mask)} punten gedetecteerd "
            f"(extra marge: {extra_margin_m:.3f} m)."
        )






class Dataset():

    def __init__(self, folder_path=DepthImageFolder):
        self.folder_path = folder_path
        self.List_of_Samples = []
        for filename in os.listdir(folder_path):
            if filename.startswith("sample"):  # Adjust the condition based on your sample naming convention
                self.List_of_Samples.append(filename)
        self.List_of_Samples.sort()  # Sort the list of samples for consistent ordering
    
    def get_random_samplepath(self):
        """Returns the path for a random sample from the dataset."""
        if not self.List_of_Samples:
            return None
        choice=np.random.choice(self.List_of_Samples)
        full_path=os.path.join(self.folder_path, choice)
        
        return full_path
    
    def get_samplepath(self,number):
        """Returns the path for a specific sample from the dataset."""
        if not self.List_of_Samples:
            return None
        if 0 <= number < len(self.List_of_Samples):
            choice = self.List_of_Samples[number]
            full_path = os.path.join(self.folder_path, choice)
            return full_path
        else:
            print("Invalid sample number.")
            return None
        
        return full_path
    
    def get_all_samplepaths(self):
        """Returns a list of all sample paths in the dataset."""
        return [os.path.join(self.folder_path, filename) for filename in self.List_of_Samples]
        
    def get_data(self, path):        
        """Returns the depth image paths, intrinsics, and extrinsics for a given sample path."""
        if path is None:
            print("No sample path provided.")
            return [], [], []
            
        metadata_path = os.path.join(path, "depth_metadata.json")
        if not os.path.exists(metadata_path):
            print(f"Metadata not found at {metadata_path}")
            return [], [], []

        with open(metadata_path, 'r') as f:
            metadata = json.load(f)

        Depth_image_paths = []
        Intrinsics = []
        Extrinsics = []
        
        width = metadata['width']
        height = metadata['height']

        for cam in metadata['cameras']:
            # 1. Retrieve Intrinsics
            intrinsic = o3d.camera.PinholeCameraIntrinsic(
                width, height, cam['fy'], cam['fy'], cam['cx'], cam['cy']           #fx is not correct so fy is needed.
            )
            Intrinsics.append(intrinsic)
            
            # 2. Retrieve Extrinsics (World to Camera Matrix)
            # Reshape the flattened 16-element list into a 4x4 matrix
            extrinsic = np.array(cam['worldToCameraMatrix']).reshape(4, 4)
            Extrinsics.append(extrinsic)
            
            # 3. Retrieve matching image path
            filename = metadata['fullDepthRawFiles'][cam['index']]
            Depth_image_paths.append(os.path.join(path, filename))
            
        return Depth_image_paths, Intrinsics, Extrinsics
    
    def get_robot_pose(self, path):
        """Returns the robot's pose (position and orientation) for a given sample path."""
        if path is None:
            print("No sample path provided.")
            return None, None
        
        pose_path = os.path.join(path, "robot_pose.json")
        if not os.path.exists(pose_path):
            print(f"Robot pose not found at {pose_path}")
            return None, None

        with open(pose_path, 'r') as f:
            metadata = json.load(f)
        

        # Retrieve the robot's position from the metadata for the 'carriage' joint
        # and convert it to a numpy array [x, y, z]
        robot_position = np.array([
            metadata["joints"]["name"=="Carriage"]["worldPosition"]["y"]-X_OFFSET,  # Unity's Y becomes Open3D's X (with offset)
            metadata["joints"]["name"=="Carriage"]["worldPosition"]["z"]-Y_OFFSET,
            metadata["joints"]["name"=="Carriage"]["worldPosition"]["x"]-Z_OFFSET
        ])

        #retrieve joints orientations
        joint_rotation={}
        for item in metadata["joints"]:
            joint_name = item["name"]
            if joint_name in JSON_NAMING_CONVENTION:
                mapped_name = JSON_NAMING_CONVENTION[joint_name]
                if joint_name == "Carriage":
                    joint_rotation[mapped_name] = 0
                else:
                    joint_rotation[mapped_name] = item["jointPosition"]

        return robot_position, joint_rotation

class RoomVoxelGrid():
    def __init__(self, resolution=default_resolution,width=default_room_dimensions[0], length=default_room_dimensions[1], height=default_room_dimensions[2]):
        self.resolution = resolution
        self.voxel_grid = o3d.geometry.VoxelGrid()

    def add_point_cloud(self, pcd):
        """Adds a point cloud to the voxel grid."""
        self.voxel_grid = o3d.geometry.VoxelGrid.create_from_point_cloud(pcd, voxel_size=self.resolution)

    def clear(self):
        self.voxel_grid.clear()

    def visualize(self,show_walls=True):
        if self.voxel_grid.is_empty():
            print("Error: Voxel grid is empty. Add some point clouds before visualizing.")
            return
        # Use the modern web/PBR viewer
        if show_walls:
            voxel_grid_copy = o3d.geometry.VoxelGrid(self.voxel_grid)  # Create a copy to modify
            
        o3d.visualization.draw([self.voxel_grid], title="Room Visualization (Voxel Grid)", show_ui=True)
    
    def get_voxel_grid(self):
        return self.voxel_grid.get_voxels()
    

    def get_info(self):
        return self.resolution
    
#-------------------------------Functions------------------------------------
def convert_pointcloud_to_voxelgrid(point_cloud: RoomPointCloud, resolution=default_resolution,include_walls=True):
    # VoxelGrid doesn't have a paint method. Instead, we paint a copy of the 
    # source point cloud so the generated voxels inherit the color.
    pcd_temp = point_cloud # Create a copy
    if not include_walls:
        pcd_temp.crop_point_cloud(x_bound=(-default_room_dimensions[0]/2, default_room_dimensions[0]/2), 
                                  y_bound=(0, default_room_dimensions[1]), 
                                  z_bound=(-default_room_dimensions[2]/2, default_room_dimensions[2]/2), 
                                  margin=-default_wall_thickness)
    pcd_temp.color_point_cloud([0.5, 0.5, 0.5])
    voxel_grid = RoomVoxelGrid(resolution=resolution)
    voxel_grid.add_point_cloud(pcd_temp.point_cloud)

    return voxel_grid

def enable_visualization():
    """Enables the Open3D visualization window."""


def unity_to_o3d_transform(unity_pos, unity_rot_deg=[-90, 90, 0]):
    # 1. Convert Unity Euler (ZXY) to Radian
    rx, ry, rz = np.radians(unity_rot_deg)
    
    # 2. Build individual matrices (Intrinsic)
    # We negate the Y and Z angles because of the handedness switch
    Rx = o3d.geometry.get_rotation_matrix_from_axis_angle([rx, 0, 0])
    Ry = o3d.geometry.get_rotation_matrix_from_axis_angle([0, -ry, 0])
    Rz = o3d.geometry.get_rotation_matrix_from_axis_angle([0, 0, -rz])
    
    # Unity's ZXY sequence: R = Ry @ Rx @ Rz
    R_final = Ry @ Rx @ Rz
    
    # 3. Handle the Position (Swap Z for Open3D if necessary)
    # Usually: Open3D_X = Unity_X, Open3D_Y = Unity_Y, Open3D_Z = Unity_Z
    # But if your PointCloud was exported with a flip, we match it here:
    t_final = np.array([unity_pos[0], unity_pos[1], unity_pos[2]])
    
    T = np.eye(4)
    T[:3, :3] = R_final
    T[:3, 3] = t_final
    return T

#-------------------------------Main Function---------------------------------
def main():

    """
    Main function to initialize the simulation, process depth data,
    and visualize the robot model and room point cloud/voxel grid.
    """
    # --- Initialization ---
    Robot_Model = RobotModel('Robotarm/hulls')
    Room_PointCloud = RoomPointCloud()
    dataset = Dataset()

    # --- Sample Path Selection ---
    # Using sample 33 for demonstration. This can be changed to a random sample
    # by calling dataset.get_random_samplepath() instead.
    sample_index = 30
    sample_path = dataset.get_samplepath(sample_index)
    print(f"Selected sample path (index {sample_index}):", sample_path)

    # --- Retrieve Camera and Robot Data ---
    robot_position, joint_rotations = dataset.get_robot_pose(sample_path)
    print("Robot Position:", robot_position)
    print("Joint Rotations:", joint_rotations)
    depth_image_paths, intrinsics, extrinsics = dataset.get_data(sample_path)

    # --- Constructing Point Cloud from Depth Images ---
    for img_path, intrinsic, extrinsic in zip(depth_image_paths, intrinsics, extrinsics):
        Room_PointCloud.add_DepthImage(img_path, intrinsic, extrinsic,add_noise=False)
    
    Room_PointCloud.crop_point_cloud(x_bound=(-default_room_dimensions[0]/2, default_room_dimensions[0]/2),
                                  y_bound=(0, default_room_dimensions[1]),
                                  z_bound=(-default_room_dimensions[2]/2, default_room_dimensions[2]/2),
                                  margin=0.5)
    
    # --- Remove Robot Model from Point Cloud ---
    T = unity_to_o3d_transform(robot_position)
    Robot_Model.apply_urdf_fk_pose(joint_values=joint_rotations, world_transform=T)
    Room_PointCloud.filter_robot_points(Robot_Model, make_red=True, extra_margin_m=0.01)

    # --- Convert Point Cloud to Voxel Grid ---
    converted_voxel_grid = convert_pointcloud_to_voxelgrid(Room_PointCloud, include_walls=True)
    robot_meshes = Robot_Model.get_all_meshes()

    # --- Visualizations ---
    Room_PointCloud.visualize()
   
    converted_voxel_grid.visualize()
    
    # Combine point cloud and robot meshes for a single visualization
    visualization_elements = [Room_PointCloud.point_cloud] + robot_meshes
    o3d.visualization.draw(visualization_elements)
 

if __name__ == "__main__":
    main()