from Test import *

def main():
    Robot_Model = RobotModel('Robotarm/hulls')
    Room_PointCloud = RoomPointCloud()
    
    dataset = Dataset()
    sample_path = dataset.get_samplepath(0)
    print("Selected sample path:", sample_path)
    robot_position= dataset.get_robot_pose(sample_path)
    print("Robot Position:",robot_position)





if __name__ == "__main__":
    main()