namespace UserGroupPermissions.Businesslogic
{

    // Namespaces.
    using ExtensionMethods;
    using Models;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Umbraco.Core;
    using Umbraco.Core.Models;
    using Umbraco.Core.Models.Membership;
    using Umbraco.Core.Persistence;


    /// <summary>
    /// Service to help with user permissions.
    /// </summary>
    public class UserTypePermissionsService
    {

        #region Readonly Variables

        private readonly Database _sqlHelper;

        #endregion


        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        public UserTypePermissionsService()
        {
            _sqlHelper = ApplicationContext.Current.DatabaseContext.Database;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets the permissions for the specified user type and the specified node.
        /// </summary>
        /// <param name="userType">
        /// The user type to get permissions for.
        /// </param>
        /// <param name="nodeId">
        /// The node ID to get permissions for.
        /// </param>
        /// <returns>
        /// The permission string.
        /// </returns>
        public string GetPermissions(IUserType userType, int nodeId)
        {

            // Variables.
            string permissions = default(string);


            // Get the permissions for the user type.
            var userTypePermissions = GetUserTypePermissions(userType, nodeId);


            // Create a permission string.
            if (userTypePermissions.Any())
            {
                var permissionCharacters = userTypePermissions.Select(x => x.PermissionId);
                permissions = string.Join(string.Empty, permissionCharacters);
            }
            else
            {

                // Use default permissions for the user type (there is a convention
                // that if no permissions were specified, the defaults should be
                // used ).
                permissions = string.Join(string.Empty, userType.Permissions);

            }


            // Return the permissions.
            return permissions;

        }


        /// <summary>
        /// Copies the user type permissions for single user.
        /// </summary>
        /// <param name="user">The user.</param>
        public void CopyPermissionsForSingleUser(IUser user)
        {
            if (!user.IsAdmin() && !user.Disabled())
            {

                // Variables.
                var nl = System.Environment.NewLine;
                var userId = user.Id;
                var userTypeId = user.UserType.Id;


                // Select all user type permissions.
                var baseQuery = new Sql()
                    .Select("UserTypePermissions.NodeID, UserTypePermissions.PermissionId")
                    .From("UserTypePermissions");

                // Join user type permissions with user permissions.
                var permissionMappingQuery = baseQuery.LeftOuterJoin("umbracoUser2NodePermission")
                    .On(new[]
                    {
                        "UserTypePermissions.NodeId = umbracoUser2NodePermission.nodeId",
                        "AND UserTypePermissions.PermissionId = umbracoUser2NodePermission.permission",
                        "AND umbracoUser2NodePermission.userId = @0"
                    }.JoinStrings(nl), userId);


                // Only include results matching the user type and that do not yet have a node
                // permission mapping for the user.
                var filteredMappingQuery = permissionMappingQuery.Where(new[]
                    {
                        "UserTypePermissions.UserTypeId = @0",
                        "AND umbracoUser2NodePermission.userId IS NULL"
                    }.JoinStrings(nl), userTypeId);


                // Query for the new permissions.
                var newPermissionResults = _sqlHelper.Query<UserTypePermissionRow>(filteredMappingQuery);


                // Only select the new permissions that haven't been assigned yet.
                var newPermissions = newPermissionResults
                    .Select(x => new User2NodePermissionDto
                    {
                        UserId = userId,
                        NodeId = x.NodeId,
                        Permission = x.PermissionId
                    }).ToArray();


                // Insert new permissions.
                _sqlHelper.BulkInsertRecords(newPermissions);

            }
        }


        /// <summary>
        /// Copies the user type permissions for one node to another.
        /// </summary>
        /// <param name="sourceNode">
        /// The source node to copy permissions from.
        /// </param>
        /// <param name="targetNode">
        /// The target node to copy permissions for.
        /// </param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void CopyNodeUserTypePermissions(IContent sourceNode, IContent targetNode)
        {
            var newNodeId = targetNode.Id;
            var query = new Sql()
                .Select("UserTypeId", "PermissionId")
                .From("UserTypePermissions")
                .Where("NodeId = @0", sourceNode.Id);
            var items = _sqlHelper.Query<UserTypePermissionRow>(query);
            items.Select(x => new UserTypePermissionRow()
            {
                NodeId = newNodeId,
                PermissionId = x.PermissionId,
                UserTypeId = x.UserTypeId
            }).ToArray();
            _sqlHelper.BulkInsertRecords(items);
        }


        /// <summary>
        /// Applies all permissions to users othe specified user type.
        /// </summary>
        /// <param name="userType">
        /// Type of the user.
        /// </param>
        /// <param name="node">
        /// The node to apply permissions to.
        /// </param>
        /// <param name="updateDescendants">
        /// Update descendants of the node too?
        /// </param>
        public void ApplyPermissions(IUserType userType, IContent node, bool updateDescendants)
        {
            var nodeIds = GetNodeIdList(node, updateDescendants).ToArray();
            DeleteNodePermissionsForUserType(userType.Id, nodeIds);
            InsertNodePermissionsForUserType(userType.Id, nodeIds);
        }


        /// <summary>
        /// Deletes all user type permissions for the specified node.
        /// </summary>
        /// <param name="node">
        /// The node to delete permissions for.
        /// </param>
        public void DeleteUserTypePermissions(IContent node)
        {
            var query = new Sql()
                .Where<UserTypePermissionRow>(x => x.NodeId == node.Id);
            _sqlHelper.Delete<UserTypePermissionRow>(query);
        }


        /// <summary>
        /// Deletes the inherited permissions from all nodes for the specified user.
        /// </summary>
        /// <param name="userId">
        /// The ID of the user.
        /// </param>
        /// <param name="oldUserType">
        /// The ID of the user's old user type.
        /// </param>
        public void DeleteNodePermissionsForUser(int userId, int oldUserType)
        {

            // Get the user type permissions corresponding the the user type.
            var selectQuery = new Sql()
                .Select("NodeId", "PermissionId")
                .From("UserTypePermissions")
                .Where("UserTypeId = @0", oldUserType);
            var items = _sqlHelper.Query<UserTypePermissionRow>(selectQuery);


            // Due to limitations of SQL Server Ce, we need to concatenate two fields
            // before performing a comparison.
            var combined = items.Select(x => x.NodeId.ToString() + "+" + x.PermissionId).ToArray();


            // Delete the permissions.
            foreach(var groupedItems in combined.InGroupsOf(1000))
            {
                var query = new Sql()
                    .Where("UserId = @0 AND (CAST(NodeId AS NVARCHAR) + '+' + Permission) IN (@1)",
                        userId, groupedItems);
                _sqlHelper.Delete<User2NodePermissionDto>(query);
            }

        }


        /// <summary>
        /// Updates the user type permissions for the specified user type, the specified node,
        /// and optionally all descendant nodes.
        /// </summary>
        /// <param name="userType">
        /// The user type to update permissions for.
        /// </param>
        /// <param name="node">
        /// The node to update permissions for.
        /// </param>
        /// <param name="permissions">
        /// The permissions to update.
        /// </param>
        /// <param name="updateDescendants">
        /// Update the descendants of the specified node too?
        /// </param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void UpdateUserTypePermissions(IUserType userType, IContent node,
            IEnumerable<char> permissions, bool updateDescendants)
        {

            // Do not act on admin user types.
            if (!userType.IsAdmin())
            {

                // Variables.
                var userTypeId = userType.Id;


                // Get node ID's (may include descendants).
                var nodeIds = GetNodeIdList(node, updateDescendants).ToArray();


                // Delete the existing user type permissions for the nodes.
                DeleteUserTypePermissions(userTypeId, nodeIds);


                // Add the new permissions for the user type.
                foreach (char permission in permissions)
                {
                    InsertGroupPermissionsForUserType(userTypeId, nodeIds, permission);
                }

            }

        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Returns the ID of the admin user type.
        /// </summary>
        /// <returns>
        /// The user type ID.
        /// </returns>
        private int GetAdminUserTypeId()
        {
            var userService = ApplicationContext.Current.Services.UserService;
            var adminUserTypeId = userService.GetUserTypeByAlias("admin").Id;
            return adminUserTypeId;
        }


        /// <summary>
        /// Returns a node ID list for the specified node.
        /// </summary>
        /// <param name="node">
        /// The node to get the list for.
        /// </param>
        /// <param name="getDescendants">
        /// Gets descendant node ID's?
        /// </param>
        /// <returns>
        /// A list of node ID's.
        /// </returns>
        /// <remarks>
        /// This queries the database for node ID's, which is more performant than using
        /// the content service.
        /// </remarks>
        private List<int> GetNodeIdList(IContent node, bool getDescendants)
        {
            var ids = new List<int>() { node.Id };
            if (getDescendants)
            {
                var pathFilter = node.Path + ",%";
                var query = new Sql().Select("id")
                    .From("umbracoNode")
                    .Where("[path] LIKE @0", pathFilter);
                var descendantIds = _sqlHelper.Query<int>(query);
                ids.AddRange(descendantIds);
            }
            return ids;
        }


        /// <summary>
        /// Deletes permission records from the umbracoUser2NodePermission table for the
        /// specified user type and specified node ID's.
        /// </summary>
        /// <param name="userTypeId">
        /// The user type to delete permissions for.
        /// </param>
        /// <param name="nodeIds">
        /// The node ID's to delete permissions from.
        /// </param>
        private void DeleteNodePermissionsForUserType(int userTypeId, int[] nodeIds)
        {

            // Get the user ID's that belong to the specified user type.
            var userQuery = new Sql()
                .Select("Id")
                .From("umbracoUser")
                .Where("userType = @0 AND Id > 0", userTypeId);
            var userIds = _sqlHelper.Query<UserDto>(userQuery).Select(x => x.Id).ToArray();


            // Process in batches to avoid running into SQL limitations.
            foreach (var groupedNodeIds in nodeIds.InGroupsOf(500))
            {
                foreach (var groupedUserIds in userIds.InGroupsOf(500))
                {

                    // Delete permissions for the appropriate users and nodes.
                    var query2 = new Sql()
                        .Where("userId IN (@0) AND nodeId IN (@1)", groupedUserIds, groupedNodeIds);
                    _sqlHelper.Delete<User2NodePermissionDto>(query2);

                }
            }

        }


        /// <summary>
        /// Inserts permission records into the umbracoUser2NodePermission for the
        /// specified user type and specified node ID's.
        /// </summary>
        /// <param name="userTypeId">
        /// The user type to insert permissions for.
        /// </param>
        /// <param name="nodeIds">
        /// The node ID's to insert permissions for.
        /// </param>
        public void InsertNodePermissionsForUserType(int userTypeId, int[] nodeIds)
        {

            // Do nothing for admins.
            if (userTypeId == GetAdminUserTypeId())
            {
                return;
            }


            // Process in batches to avoid SQL limitations.
            foreach (var groupedNodeIds in nodeIds.InGroupsOf(1000))
            {
                var query = new Sql()
                    .Select("c.id userId", "a.nodeId", "a.PermissionId permission")
                    .From("UserTypePermissions a")
                    .InnerJoin("umbracoUser c")
                    .On("a.UserTypeId = c.userType")
                    .LeftOuterJoin("umbracoUser2NodePermission b")
                    .On("a.NodeId = b.nodeId AND a.PermissionId = b.permission AND b.userId = c.id")
                    .Where("a.UserTypeId = @0 AND a.NodeId IN (@1) AND b.userId IS NULL AND c.id > 0",
                        userTypeId, groupedNodeIds);
                var rows = _sqlHelper.Query<User2NodePermissionDto>(query).ToArray();
                _sqlHelper.BulkInsertRecords(rows);
            }

        }


        /// <summary>
        /// Inserts permissions for the specified nodes. Does not check for existing permissions.
        /// </summary>
        /// <param name="userTypeId">
        /// The ID of the user type to insert permissions for.
        /// </param>
        /// <param name="nodeIds">
        /// The node ID's to insert permissions for.
        /// </param>
        /// <param name="permissionKey">
        /// The permission to insert.
        /// </param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void InsertGroupPermissionsForUserType(int userTypeId, int[] nodeIds, char permissionKey)
        {
            foreach (var groupedIds in nodeIds.InGroupsOf(1000))
            {

                // Select only node ID's that exist.
                var query = new Sql()
                    .Select("Id")
                    .From("umbracoNode")
                    .Where("Id In (@0)", groupedIds);
                var nodes = _sqlHelper.Query<NodeDto>(query);


                // Create rows to be inserted.
                var rows = nodes.Select(x => new UserTypePermissionRow()
                {
                    NodeId = x.NodeId,
                    PermissionId = permissionKey.ToString(),
                    UserTypeId = userTypeId
                }).ToArray();


                // Bulk insert the rows.
                _sqlHelper.BulkInsertRecords(rows);

            }
        }


        /// <summary>
        /// Deletes all permissions for the node/user type combination.
        /// </summary>
        /// <param name="userTypeId">
        /// The ID of the user type to delete permissions for.
        /// </param>
        /// <param name="nodeIds">
        /// The ID's of the nodes to delete permissions for.
        /// </param>
        private void DeleteUserTypePermissions(int userTypeId, int[] nodeIds)
        {
            foreach (var groupedIds in nodeIds.InGroupsOf(1000))
            {
                var query = new Sql()
                    .Where("NodeId IN (@0) AND UserTypeId = @1", groupedIds, userTypeId);
                _sqlHelper.Delete<UserTypePermissionRow>(query);
            }
        }


        /// <summary>
        /// Returns the permissions for a user type and node.
        /// </summary>
        /// <param name="userType">
        /// The user type.
        /// </param>
        /// <param name="nodeId">
        /// The node ID to get permissions for.
        /// </param>
        /// <returns>
        /// The permissions.
        /// </returns>
        private IEnumerable<UserTypePermissionRow> GetUserTypePermissions(IUserType userType, int nodeId)
        {
            var query = new Sql()
                .Select("*")
                .From("UserTypePermissions")
                .Where("UserTypeId = @0 AND NodeId = @1", userType.Id, nodeId);
            var items = _sqlHelper.Fetch<UserTypePermissionRow>(query);
            return items.ToArray();
        }

        #endregion

    }

}