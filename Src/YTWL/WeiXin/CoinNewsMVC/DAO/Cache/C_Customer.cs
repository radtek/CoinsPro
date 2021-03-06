﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DAO.DAL;
using DAO.IDAL;
using NHibernate.Criterion;
using Domain;
using Common;
using CacheRedis;
using System.Web.Configuration;
namespace DAO.Cache {
	/// <summary>
	/// 缓存层D_Customer
	/// </summary>
	public class C_Customer:D_Customer
	{
        //类名 + 方法名 + 参数

        //是否启动主从
        private static bool IsSlave = Convert.ToBoolean(WebConfigurationManager.AppSettings["isslave"]);
        
        //类名
        private string className = System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName;
        
        //redis对象
        private RedisHelper rh = new RedisHelper(IsSlave);
        
        
		#region  成员方法
		/// <summary>
		/// 是否存在该记录
		/// </summary>
		public override bool Exists(object id)
		{
			string key = "Customer:" + id;
			var res = rh.Exist(key);
            if (res > 0)//存在
            {
                return true;
            }
            else 
            {
                var model = base.Get(id);
                if (model != null) 
                {
                    rh.Set<Customer>(key, model);
                    return true;
                }
                return false;
            }
		}
		/// <summary>
		/// 增加一条数据
		/// </summary>
		public override object  Save(Customer model)
		{
			var res = base.Save(model);
            if ((int)res > 0) 
            {
                string key = "Customer:" + res;

                if (rh.Exist(key) <= 0) //不存在
                {
                    rh.Set<Customer>(key, model);
                    //检查是否存在关联
                    rh.DelJoin("Customer");
                }
            }
            return res;
		}
		/// <summary>
		/// 更新一条数据
		/// </summary>
		public override void Update(Customer model)
		{
			string key = "Customer:" + model.Id;
            if (rh.Exist(key) > 0) //存在
            {
                //修改当前集合
                rh.Set<Customer>(key, model);
                //检查是否存在关联
                rh.DelJoin("Customer");
            }
            base.Update(model);
		}
		/// <summary>
		/// 删除数据
		/// </summary>
		public override void Delete(object id)
		{
			string key = "Customer:" + id;
            if (rh.Exist(key) > 0) //存在
            {
                //修改当前集合
                rh.Remove(key);
                //检查是否存在关联
                rh.DelJoin("Customer");
            }
            base.Delete(id);
		}

		/// <summary>
		/// 删除数据
		/// </summary>
		public override void Delete(Customer model)
		{
			 string key = "Customer:" + model.Id;
            if (rh.Exist(key) > 0) //存在
            {
                rh.Remove(key);
                //检查是否存在关联
                rh.DelJoin("Customer");
            }
            base.Delete(model);
		}

		/// <summary>
		/// 得到一个对象实体
		/// </summary>
		public override Customer Get(object id)
		{
			string key = "Customer:" + id;
            if (rh.Exist(key) > 0) //存在
            {
                return rh.Get<Customer>(key);
            }
            else
            {
                var model = base.Get(id);
                rh.Set<Customer>(key, model);
                return model;
            }
		}
		
		/// <summary>
		/// 获得数据列表
		/// </summary>
		public override IList<Customer> LoadAll()
		{
			var functionName = System.Reflection.MethodBase.GetCurrentMethod().Name;
            var key = className + ":" + functionName;
            //添加关联
            rh.AddJoin("Customer", key);
            if (rh.Exist(key) > 0) //存在
            {
                return rh.GetList<Customer>(key) as IList<Customer>;
            }
            else 
            {
                IList<Customer> list = base.LoadAll();
                //写入缓存
                rh.AddList<Customer>(key, list);
                return list;
            }
		}

		public override IList<Customer>  GetList(List<SearchTemplate> st, List<Order> order)
		{
			    var functionName = System.Reflection.MethodBase.GetCurrentMethod().Name;
	            var key = className + ":" + functionName + ":" + rh.GetString<SearchTemplate>(st) + ":" + rh.GetString(order); 
	            //添加关联
	            rh.AddJoin("Customer", key);
	            if (rh.Exist(key) > 0) //存在
	            {
	                return rh.GetList<Customer>(key) as IList<Customer>;
	            }
	            else
	            {
	                IList<Customer> list = base.GetList(st, order);
	                //写入缓存
	                rh.AddList<Customer>(key, list);
	                return list;
	            }
		}
		
		/// <summary>
        /// 获取总行数
        /// </summary>
        /// <param name="parent_id"></param>
        /// <returns></returns>
        public override int GetCount(List<SearchTemplate> st)
        {
            var functionName = System.Reflection.MethodBase.GetCurrentMethod().Name;
            var key = className + ":" + functionName + ":" + rh.GetString<SearchTemplate>(st);
            //添加关联
            rh.AddJoin("Customer", key);
            if (rh.Exist(key) > 0) //存在
            {
                return rh.Get<int>(key);
            }
            else
            {
                int count = base.GetCount(st);
                //写入缓存
                rh.Set<int>(key, count);
                return count;
            }
        }
		#endregion  成员方法
	} 
}