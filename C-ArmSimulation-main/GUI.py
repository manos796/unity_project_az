from re import split

from Test import *
import open3d.visualization.gui as gui
import open3d.visualization.rendering as rendering


class RobotVizTool:
    def __init__(self,dataset: Dataset):
        # 1. Initialize the Application
        self.dataset = dataset
        self.app = gui.Application.instance
        self.app.initialize()

        # 2. Create the Window
        self.window = self.app.create_window("Robot Scan Inspector", 1200, 800)
        
        # Theme/Styling
        em = self.window.theme.font_size
        margin = 0.5 * em
        
        # 3. Create the 3D Scene Widget
        self.scene_widget = gui.SceneWidget()
        self.scene_widget.scene = rendering.Open3DScene(self.window.renderer)
        self.scene_widget.scene.set_background([0.9, 0.9, 0.9, 1]) # Light grey

        # 4. Create the Settings Panel (Sidebar)
        self.panel = gui.Vert(0, gui.Margins(margin, margin, margin, margin))
        
        # --- Dropdown: Select Sample ---
        self.panel.add_child(gui.Label("Select Sample"))
        self.sample_select = gui.Combobox()
        samples=dataset.get_all_samplepaths()
        for sample in samples:
            self.sample_select.add_item(sample)
        self.sample_select.set_on_selection_changed(self._on_sample_changed)
        self.panel.add_child(self.sample_select)

        self.panel.add_fixed(margin)

        # --- Dropdown: View Type ---
        self.panel.add_child(gui.Label("View Mode"))
        self.view_select = gui.Combobox()
        self.view_select.add_item("Depth Images")
        self.view_select.add_item("PointCloud")
        self.view_select.add_item("PointCloud + Robot")
        self.view_select.add_item("Voxel grid")
        self.view_select.set_on_selection_changed(self._update_scene)
        self.panel.add_child(self.view_select)

        self.panel.add_fixed(margin)

        # --- Checkbox: Show Axes ---
        self.show_axes = gui.Checkbox("Show Coordinate Axes")
        self.show_axes.set_on_checked(self._on_axes_toggled)
        self.panel.add_child(self.show_axes)

        # --- Checkbox: Crop walls ---
        self.crop_walls = gui.Checkbox("Crop Walls")
        self.panel.add_child(self.crop_walls)
        self.crop_walls.set_on_checked(self._on_checkbox)

        #--- Checkbox: Remove robot---
        self.remove_robot = gui.Checkbox("Remove Robot")
        self.panel.add_child(self.remove_robot)
        self.remove_robot.set_on_checked(self._on_checkbox)

        #---Checkbox: Show robot URDF ---
        self.show_robot_urdf = gui.Checkbox("Show Robot URDF")
        self.panel.add_child(self.show_robot_urdf)
        self.show_robot_urdf.set_on_checked(self._on_checkbox)


        #--- Checkbox: Add noise ---
        self.add_noise = gui.Checkbox("Add Noise")
        self.panel.add_child(self.add_noise)
        self.add_noise.set_on_checked(self._on_checkbox)


        # 5. Layout Setup
        self.window.set_on_layout(self._on_layout)
        self.window.add_child(self.scene_widget)
        self.window.add_child(self.panel)

        

        # Create the Image Widget
        self.unity_screenshot = gui.ImageWidget()
        # Optional: Add a nice border or background
        self.unity_screenshot.background_color = gui.Color(0, 0, 0, 1) 
        #self.unity_screenshot.ui_scaling_filter = gui.ImageWidget.Scaling.ASPECT_FIT
        # Add it to the window (as a child of the window, not the sidebar)
        self.window.add_child(self.unity_screenshot)
        
        # Hide it by default until a sample is loaded
        self.unity_screenshot.visible = True

        self.grid_images = []
        for i in range(4):
            img_widget = gui.ImageWidget()
            img_widget.visible = False # Hide them by default
            self.window.add_child(img_widget)
            self.grid_images.append(img_widget)

        # Initial Data Load
        self._load_dummy_data()
        self._update_scene(None, 0)

    def _on_checkbox(self,value):
        self._update_scene(None,0)


    def _on_layout(self, layout_context):
        content_rect = self.window.content_rect
        em = layout_context.theme.font_size
        panel_width = 18 * em  # Sidebar width
        
        # 1. Define the Sidebar Frame (Right side, full height)
        self.panel.frame = gui.Rect(content_rect.get_right() - panel_width, 
                                content_rect.y, panel_width, content_rect.height)
        
        # Calculate available width for the main area (Left of sidebar)
        main_width = content_rect.width - panel_width
        main_height = content_rect.height
        
        # 2. Define the Split Point (e.g., 65% height for 3D, 35% for Image)
        split_y = int(content_rect.height * 0.65)
        
        # 3. 3D Scene Frame (Top Part)
        self.scene_widget.frame = gui.Rect(content_rect.x, 
                                        content_rect.y, 
                                        main_width, 
                                        split_y)
        
        # 4. Screenshot Frame (Bottom Part)
        # We add a small margin (5px) so they don't touch perfectly
        margin = 5
        self.unity_screenshot.frame = gui.Rect(content_rect.x + margin, 
                                            split_y + margin, 
                                            main_width - (2 * margin), 
                                            content_rect.height - split_y - (2 * margin))
            # 4-Image Grid Logic
        half_w = main_width // 2
        half_h = split_y // 2
        
        # Top-Left
        self.grid_images[0].frame = gui.Rect(content_rect.x, content_rect.y, half_w, half_h)
        # Top-Right
        self.grid_images[1].frame = gui.Rect(content_rect.x + half_w, content_rect.y, half_w, half_h)
        # Bottom-Left
        self.grid_images[2].frame = gui.Rect(content_rect.x, content_rect.y + half_h, half_w, half_h)
        # Bottom-Right
        self.grid_images[3].frame = gui.Rect(content_rect.x + half_w, content_rect.y + half_h, half_w, half_h)

    def _load_dummy_data(self):
        # In your real code, load your .ply and .obj here
        self.pcd = o3d.geometry.PointCloud()
        self.pcd.points = o3d.utility.Vector3dVector(np.random.uniform(-1, 1, (1000, 3)))
        
        self.mesh = o3d.geometry.TriangleMesh.create_coordinate_frame(size=0.5)

    def _on_sample_changed(self, name, index):
        print(f"Selected sample changed: Name='{name}', Index={index}")
        print(f"Loading Sample: {name}")
        # Logic to load your specific JSON/PCD files goes here
        image_path=os.path.join(self.sample_select.selected_text,"cam0_depth_vis.png")
        print(image_path)
        try:
            new_img = o3d.io.read_image(image_path)
            self.unity_screenshot.update_image(new_img)
            self.unity_screenshot.visible = True
        except:
            print(f"Warning: Could not load image at {image_path}")
            self.unity_screenshot.visible = False
        self._update_scene(name, index)
        

    def _on_axes_toggled(self, is_checked):
        self.scene_widget.scene.show_axes(is_checked)

    def _update_scene(self, name, index):
        self.scene_widget.scene.clear_geometry() # Clear previous geometry
        
        view_mode = self.view_select.selected_text
        sample=self.sample_select.selected_text
    
        if view_mode == "Depth Images":
            self.scene_widget.visible = False
            for i, img_widget in enumerate(self.grid_images):
                img_widget.visible = True
                # Load your specific PNGs here (e.g., Top, Side, Front, Perspective)

                path=os.path.join(sample,f"cam{i}_depth_vis.png")
                img_widget.update_image(o3d.io.read_image(path))
        else:
            self.scene_widget.visible = True
            for img_widget in self.grid_images:
                img_widget.visible = False  
            mat = rendering.MaterialRecord() # Basic material
            # 'defaultLit' is what makes the points look "3D" and shaded
            mat.shader = "defaultLit" 

            # This is the secret for making point clouds look like draw_geometries:
            # It tells the shader to use the normals for lighting calculations
            mat.base_color = [0.8, 0.8, 0.8, 1.0] # Soft grey
            mat.point_size = 2.0 * self.window.scaling # Adjust for high-DPI screens
            mat_mesh = rendering.MaterialRecord()
            mat_mesh.shader = "defaultLit"
            mat_mesh.base_color = [1, 1, 1, 1]

            mat_voxel = rendering.MaterialRecord()
            mat_voxel.shader = "defaultLit"  # This provides the 3D depth for each cube
            mat_voxel.base_color = [0.4, 0.6, 0.9, 1.0]  # A nice blue-ish tint
            if sample == None:
                return
            #Initialization
            Robot_Model = RobotModel('Robotarm/hulls')
            Room_PointCloud = RoomPointCloud()

            #Retrieve data
            robot_position, joint_rotations = self.dataset.get_robot_pose(sample)
            depth_image_paths, intrinsics, extrinsics = self.dataset.get_data(sample)
        
            for img_path, intrinsic, extrinsic in zip(depth_image_paths, intrinsics, extrinsics):
                Room_PointCloud.add_DepthImage(img_path, intrinsic, extrinsic,add_noise=self.add_noise.checked)

            Room_PointCloud.crop_point_cloud(x_bound=(-default_room_dimensions[0]/2, default_room_dimensions[0]/2),
                                    y_bound=(0, default_room_dimensions[1]),
                                    z_bound=(-default_room_dimensions[2]/2, default_room_dimensions[2]/2),
                                    margin=0.5)
            
        
            # --- Remove Robot Model from Point Cloud ---


            # --- Convert Point Cloud to Voxel Grid ---
            
            

            visualization_elements=[]
            if "PointCloud"==view_mode or "Overlay" in view_mode:
                visualization_elements.append(("pcd",Room_PointCloud.point_cloud,mat))

            elif "PointCloud + Robot"==view_mode:
                T = unity_to_o3d_transform(robot_position)
                Robot_Model.apply_urdf_fk_pose(joint_values=joint_rotations, world_transform=T)
                Room_PointCloud.filter_robot_points(Robot_Model, make_red= not self.remove_robot.checked, extra_margin_m=0.01)
                visualization_elements.append(("pcd",Room_PointCloud.point_cloud,mat))
                if self.show_robot_urdf.checked:
                    robot_meshes = Robot_Model.get_all_meshes()
                    for i, mesh in enumerate(robot_meshes):
                        visualization_elements.append((f"robot_part_{i}", mesh, mat_mesh))

            if "Voxel grid" in view_mode:
                converted_voxel_grid = convert_pointcloud_to_voxelgrid(Room_PointCloud, include_walls= not self.crop_walls.checked)
                
                visualization_elements.append(("voxel_grid",converted_voxel_grid.voxel_grid,mat_voxel))
            
            
            for uid, geom, mat in visualization_elements:
                self.scene_widget.scene.add_geometry(uid, geom, mat)
        self.window.set_needs_layout()

        # Reset camera to see the object
        #bounds = self.pcd.get_axis_aligned_bounding_box()
        #self.scene_widget.setup_camera(60, bounds, bounds.get_center())

    def run(self):
        self.app.run()

if __name__ == "__main__":
    dataset = Dataset()
    tool = RobotVizTool(dataset)
    tool.run()
